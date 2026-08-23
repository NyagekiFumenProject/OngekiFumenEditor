#nullable enable

using OngekiFumenEditor.Avalonia.Kernel.Audio;
using OngekiFumenEditor.Avalonia.Platforms.Services.FileSystem.Providers;
using OngekiFumenEditor.Avalonia.Utils.SimpleFileSystem;

namespace OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Setup;

public enum AwbImportAction
{
    BindExisting,
    CommitNew,
    ReplaceExisting
}

public sealed record AwbReplaceCandidate(
    string ExistingAwbFullPath,
    string PickedAwbFullPath,
    long ExistingAwbLength,
    long PickedAwbLength);

/// <summary>
///     UI-facing interaction points of the AWB import transaction. The importer stays
///     platform-agnostic; the caller supplies the picker and the replace confirmation.
/// </summary>
public sealed record ExternalAwbImportCallbacks(
    Func<string, CancellationToken, Task<ISimpleFile?>> PickExternalAwbAsync,
    Func<AwbReplaceCandidate, CancellationToken, Task<bool>> ConfirmReplaceAsync);

public sealed record ExternalAwbImportResult(AwbImportAction Action, ISimpleFile BoundAwbFile);

/// <summary>
///     Implements the folder-open external AWB transaction:
///     reuse an existing decodable project AWB, otherwise pick an authoritative external AWB,
///     stage it in temporary storage, verify that ACB + staged AWB decode, compare against the
///     existing project AWB when present, and only then commit into the project directory.
///     Any failure or cancellation leaves the existing project AWB untouched; a half-created
///     target file is removed again. There is deliberately no "keep using the external AWB"
///     fallback once the copy path failed.
/// </summary>
public static class ExternalAwbImporter
{
    public delegate Task DecodeVerifier(
        Stream acbStream,
        Stream awbStream,
        CancellationToken cancellationToken);

    public static async Task<ExternalAwbImportResult?> ImportAsync(
        ISimpleFile acbFile,
        string expectedAwbFileName,
        ISimpleDirectory? fallbackParentDirectory,
        ExternalAwbImportCallbacks callbacks,
        DecodeVerifier? decodeVerifier = null,
        ITemporaryFolderProvider? temporaryFolderProvider = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(acbFile);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedAwbFileName);
        ArgumentNullException.ThrowIfNull(callbacks);
        PortableEntryNameValidator.ThrowIfInvalid(expectedAwbFileName, nameof(expectedAwbFileName));
        Log.LogInfo($"Importing external AWB '{expectedAwbFileName}' for audio '{acbFile.FileName}'.");

        var parentDirectory = acbFile.ParentDictionary ?? fallbackParentDirectory ??
            throw new InvalidDataException($"Audio '{acbFile.FileName}' is not attached to the project directory.");

        var siblingMatches = parentDirectory.ChildFiles
            .Where(file => file.FileName.Equals(expectedAwbFileName, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (siblingMatches.Length > 1)
        {
            throw new InvalidDataException(
                $"Audio '{acbFile.FileName}' has multiple AWB candidates named '{expectedAwbFileName}'.");
        }
        var existing = siblingMatches.Length == 1 ? siblingMatches[0] : null;

        var verifier = decodeVerifier ?? DefaultVerifyDecodableAsync;

        // An existing project AWB that decodes cleanly is reused as-is; nothing is imported.
        if (existing is not null &&
            await TryVerifyDecodableAsync(verifier, acbFile, existing, cancellationToken).ConfigureAwait(false))
        {
            Log.LogInfo($"External AWB import bound existing AWB '{existing.FileName}' for '{acbFile.FileName}'.");
            return new ExternalAwbImportResult(AwbImportAction.BindExisting, existing);
        }

        // No usable project AWB: the user must supply the authoritative external AWB. Its name
        // has to match the ACB declaration exactly; the picker contract enforces that.
        var picked = await callbacks.PickExternalAwbAsync(expectedAwbFileName, cancellationToken).ConfigureAwait(false);
        if (picked is null)
        {
            Log.LogInfo("External AWB import canceled: no external AWB file was picked.");
            return null;
        }

        var storage = temporaryFolderProvider ?? IoC.Get<ITemporaryFolderProvider>();
        if (!storage.IsAvailable)
            throw new PlatformNotSupportedException("AWB staging requires temporary file storage on this platform.");

        ISimpleFile? staging = null;
        ISimpleFile? createdTarget = null;
        try
        {
            staging = await storage
                .CreateUniqueFileAsync("awbStaging", ".awb", cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            await picked.CopyContentToAsync(staging, cancellationToken).ConfigureAwait(false);

            try
            {
                await VerifyWithStreamsAsync(verifier, acbFile, staging, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                throw new InvalidDataException(
                    $"The selected AWB cannot be decoded together with '{acbFile.FileName}': {exception.Message}",
                    exception);
            }

            if (existing is not null)
            {
                var contentsEqual = await AwbContentComparer
                    .AreContentsEqualAsync(staging, existing, cancellationToken)
                    .ConfigureAwait(false);
                if (contentsEqual)
                {
                    // Identical bytes mean the earlier verification failure was transient;
                    // per Q3 identical content reuses the project copy without any change.
                    Log.LogInfo($"External AWB import bound existing AWB '{existing.FileName}' for '{acbFile.FileName}' (identical content).");
                    return new ExternalAwbImportResult(AwbImportAction.BindExisting, existing);
                }

                var confirmed = await callbacks.ConfirmReplaceAsync(
                    new AwbReplaceCandidate(
                        existing.FullPath,
                        picked.FullPath,
                        await GetLengthOrZeroAsync(existing, cancellationToken).ConfigureAwait(false),
                        await GetLengthOrZeroAsync(picked, cancellationToken).ConfigureAwait(false)),
                    cancellationToken).ConfigureAwait(false);
                if (!confirmed)
                    return null;

                // CopyTo + Delete replacement: the transactional commit keeps the old content
                // on failure, and the staging file is removed after a successful commit.
                await staging.CopyContentToAsync(existing, cancellationToken).ConfigureAwait(false);
                await DeleteQuietlyAsync(staging).ConfigureAwait(false);
                staging = null;
                Log.LogInfo($"External AWB import replaced existing AWB '{existing.FileName}' for '{acbFile.FileName}'.");
                return new ExternalAwbImportResult(AwbImportAction.ReplaceExisting, existing);
            }

            createdTarget = await parentDirectory
                .CreateFileAsync(expectedAwbFileName, cancellationToken)
                .ConfigureAwait(false);
            await staging.CopyContentToAsync(createdTarget, cancellationToken).ConfigureAwait(false);
            await DeleteQuietlyAsync(staging).ConfigureAwait(false);
            staging = null;
            Log.LogInfo($"External AWB import committed new AWB '{createdTarget.FileName}' for '{acbFile.FileName}'.");
            return new ExternalAwbImportResult(AwbImportAction.CommitNew, createdTarget);
        }
        catch (Exception exception)
        {
            Log.LogError($"External AWB import failed for audio '{acbFile.FileName}'.", exception);
            // Q5: never leave a half-written project AWB behind. Replacing commits keep the old
            // content by themselves; only the newly created placeholder needs explicit rollback.
            if (createdTarget is not null)
                await DeleteQuietlyAsync(createdTarget).ConfigureAwait(false);
            throw;
        }
        finally
        {
            if (staging is not null)
                await DeleteQuietlyAsync(staging).ConfigureAwait(false);
        }
    }

    private static async Task<bool> TryVerifyDecodableAsync(
        DecodeVerifier verifier,
        ISimpleFile acbFile,
        ISimpleFile awbFile,
        CancellationToken cancellationToken)
    {
        try
        {
            await VerifyWithStreamsAsync(verifier, acbFile, awbFile, cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            Log.LogInfo($"The project AWB '{awbFile.FullPath}' did not decode with its ACB: {exception.Message}");
            return false;
        }
    }

    private static async Task VerifyWithStreamsAsync(
        DecodeVerifier verifier,
        ISimpleFile acbFile,
        ISimpleFile awbFile,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await using var acbStream = await acbFile.OpenReadAsync(cancellationToken).ConfigureAwait(false);
        await using var awbStream = await awbFile.OpenReadAsync(cancellationToken).ConfigureAwait(false);
        await verifier(acbStream, awbStream, cancellationToken).ConfigureAwait(false);
    }

    private static async Task DefaultVerifyDecodableAsync(
        Stream acbStream,
        Stream awbStream,
        CancellationToken cancellationToken)
    {
        var temporaryFolderProvider = IoC.Get<ITemporaryFolderProvider>();
        if (!temporaryFolderProvider.IsAvailable)
            throw new PlatformNotSupportedException("AWB verification requires temporary file storage.");

        var wavOutput = await temporaryFolderProvider
            .CreateUniqueFileAsync("awbVerify", ".wav", cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        try
        {
            await AcbConverter.ConvertAcbFileToWavAsync(
                acbStream,
                awbStream,
                wavOutput,
                cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            await DeleteQuietlyAsync(wavOutput).ConfigureAwait(false);
        }
    }

    private static Task<long> GetLengthOrZeroAsync(ISimpleFile file, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(file);
        return file.GetLengthAsync(cancellationToken);
    }

    private static async Task DeleteQuietlyAsync(ISimpleFile file)
    {
        try
        {
            await file.DeleteAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            Log.LogWarn($"Unable to delete temporary file '{file.FullPath}': {exception.Message}");
        }
    }
}
