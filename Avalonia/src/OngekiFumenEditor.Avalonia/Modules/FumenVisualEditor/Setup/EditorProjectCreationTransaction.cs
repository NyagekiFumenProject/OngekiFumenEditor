#nullable enable

using System.Buffers;
using OngekiFumenEditor.Avalonia.Base;
using OngekiFumenEditor.Avalonia.Kernel.Audio;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Base;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Kernel.EditorProjectFile;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Models;
using OngekiFumenEditor.Avalonia.Parser;
using OngekiFumenEditor.Avalonia.Utils.SimpleFileSystem;

namespace OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Setup;

public enum EditorProjectCreationPhase
{
    WaitingForIoGate,
    RefreshingDirectory,
    ValidatingSelection,
    ParsingSourceFumen,
    InspectingAudioPackage,
    DecodingSourceAudio,
    PreparingNewFumen,
    PreparingProjectData,
    CopyingExternalAwb,
    CopyingAudio,
    CopyingFumen,
    WritingNewFumen,
    WritingProjectFile,
    VerifyingCreatedFiles,
    LoadingCandidateContext,
    AttachingEditor,
    RollingBack
}

public sealed record EditorProjectCreationProgress(
    EditorProjectCreationPhase Phase,
    string? CurrentFileName = null,
    long BytesCompleted = 0,
    long? TotalBytes = null);

public enum EditorProjectCreationFailureKind
{
    InvalidSelection,
    TargetConflict,
    UnsupportedFormat,
    InvalidAudioPackage,
    SourceReadFailed,
    TargetWriteFailed,
    VerificationFailed,
    EditorRejected,
    Unknown
}

public sealed class EditorProjectCreationException : Exception
{
    public EditorProjectCreationException(
        EditorProjectCreationFailureKind kind,
        string message,
        Exception? innerException = null)
        : base(message, innerException)
    {
        Kind = kind;
    }

    public EditorProjectCreationFailureKind Kind { get; }
}

public abstract record EditorProjectCreationOutcome
{
    private EditorProjectCreationOutcome()
    {
    }

    public sealed record Succeeded : EditorProjectCreationOutcome;

    public sealed record Canceled(IReadOnlyList<string> RollbackFailures)
        : EditorProjectCreationOutcome;

    public sealed record Failed(
        EditorProjectCreationFailureKind Kind,
        Exception Exception,
        IReadOnlyList<string> RollbackFailures)
        : EditorProjectCreationOutcome;
}

/// <summary>
/// Owns a frozen creation plan until the editor accepts the candidate context. Only files
/// returned by CreateFileAsync are eligible for rollback.
/// </summary>
public sealed class EditorProjectCreationTransaction : IDisposable
{
    private readonly EditorProjectCreationPlan plan;
    private readonly IFumenParserManager parserManager;
    private readonly IAudioManager audioManager;
    private readonly EditorProjectFileManager projectFileManager = new();
    private readonly List<ISimpleFile> createdFiles = [];
    private IDisposable? ioLease;
    private EditorFileAccessContext? candidateFileContext;
    private EditorContext? candidateContext;
    private bool projectRootTransferred;
    private bool completed;
    private bool disposed;

    public EditorProjectCreationTransaction(
        EditorProjectCreationPlan plan,
        IFumenParserManager parserManager,
        IAudioManager audioManager)
    {
        this.plan = plan ?? throw new ArgumentNullException(nameof(plan));
        this.parserManager = parserManager ?? throw new ArgumentNullException(nameof(parserManager));
        this.audioManager = audioManager ?? throw new ArgumentNullException(nameof(audioManager));
    }

    public IReadOnlyList<ISimpleFile> CreatedFiles => createdFiles;

    public async Task<EditorContext> PrepareAsync(
        IProgress<EditorProjectCreationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (ioLease is not null || completed)
            throw new InvalidOperationException("The creation transaction has already been started.");

        Report(progress, EditorProjectCreationPhase.WaitingForIoGate);
        ioLease = await EditorProjectIoGate.EnterAsync(cancellationToken);

        var selection = plan.Selection;
        try
        {
            Report(progress, EditorProjectCreationPhase.ValidatingSelection);
            ValidateFrozenSelection(selection);

            Report(progress, EditorProjectCreationPhase.InspectingAudioPackage, selection.AudioFile.FileName);
            var packageInspection = await AcbPackageInspector.InspectAsync(
                selection.AudioFile,
                cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            ValidateAudioPackage(selection, packageInspection);

            Report(progress, EditorProjectCreationPhase.DecodingSourceAudio, selection.AudioFile.FileName);
            var audioDuration = await ValidateAudioAsync(selection, cancellationToken);

            byte[]? newFumenBytes = null;
            if (selection.FumenMode == SetupFumenMode.CreateNew)
            {
                Report(progress, EditorProjectCreationPhase.PreparingNewFumen, selection.NewFumenFileName);
                newFumenBytes = await PrepareNewFumenAsync(selection, cancellationToken);
            }
            else
            {
                Report(progress, EditorProjectCreationPhase.ParsingSourceFumen,
                    selection.ExistingFumenFile?.FileName);
                await ValidateExistingFumenAsync(selection, cancellationToken);
            }

            Report(progress, EditorProjectCreationPhase.PreparingProjectData, selection.ProjectFileName);
            var projectBytes = await PrepareProjectDataAsync(audioDuration, cancellationToken);

            Report(progress, EditorProjectCreationPhase.RefreshingDirectory);
            await ValidateTargetConflictsAsync(selection.ProjectDirectory, cancellationToken);

            var finalFiles = plan.ExistingBindings.ToDictionary(binding => binding.Role, binding => binding.ProjectFile);
            foreach (var copy in OrderCopyPlans(plan.FilesToCopy))
            {
                var phase = copy.Role switch
                {
                    EditorProjectFileRole.AudioAwb => EditorProjectCreationPhase.CopyingExternalAwb,
                    EditorProjectFileRole.Audio => EditorProjectCreationPhase.CopyingAudio,
                    EditorProjectFileRole.Fumen => EditorProjectCreationPhase.CopyingFumen,
                    _ => throw new InvalidOperationException($"Unexpected copy role '{copy.Role}'.")
                };
                finalFiles[copy.Role] = await CopyFileAsync(
                    selection.ProjectDirectory,
                    copy,
                    phase,
                    progress,
                    cancellationToken);
            }

            if (selection.FumenMode == SetupFumenMode.CreateNew)
            {
                finalFiles[EditorProjectFileRole.Fumen] = await CreateAndWriteAsync(
                    selection.ProjectDirectory,
                    selection.NewFumenFileName!,
                    newFumenBytes!,
                    EditorProjectCreationPhase.WritingNewFumen,
                    progress,
                    cancellationToken);
            }

            var projectFile = await CreateAndWriteAsync(
                selection.ProjectDirectory,
                selection.ProjectFileName,
                projectBytes,
                EditorProjectCreationPhase.WritingProjectFile,
                progress,
                cancellationToken);
            finalFiles[EditorProjectFileRole.Project] = projectFile;

            cancellationToken.ThrowIfCancellationRequested();
            Report(progress, EditorProjectCreationPhase.VerifyingCreatedFiles);
            EnsureRequiredRoles(finalFiles);

            // Ownership moves from Selection to the candidate context before the factory call.
            // If construction fails, the factory disposes the transferred root itself.
            selection.TransferProjectDirectory();
            projectRootTransferred = true;
            candidateFileContext = EditorFileAccessContext.Create(
                selection.ProjectDirectory,
                projectFile: finalFiles[EditorProjectFileRole.Project],
                fumenFile: finalFiles[EditorProjectFileRole.Fumen],
                audioFile: finalFiles[EditorProjectFileRole.Audio],
                audioAwbFile: finalFiles.GetValueOrDefault(EditorProjectFileRole.AudioAwb));

            Report(progress, EditorProjectCreationPhase.LoadingCandidateContext);
            using var loadedData = await EditorProjectDataUtils.LoadDataAsync(
                candidateFileContext,
                cancellationToken,
                parserManager);
            cancellationToken.ThrowIfCancellationRequested();
            var (projectData, fumen) = loadedData.Take();
            candidateContext = new EditorContext
            {
                ProjectData = projectData,
                Fumen = fumen,
                ProjectName = selection.ProjectName,
                LocationDescription = BuildLocationDescription(selection),
                FileName = selection.ProjectFileName,
                FilePath = projectFile.FullPath,
                FileAccessContext = candidateFileContext
            };
            candidateFileContext = null;
            return candidateContext;
        }
        catch (EditorProjectCreationException)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new EditorProjectCreationException(
                EditorProjectCreationFailureKind.Unknown,
                $"Unable to create the project: {exception.Message}",
                exception);
        }
    }

    public void Commit()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (candidateContext is null || completed)
            throw new InvalidOperationException("There is no prepared candidate to commit.");

        // The editor owns the candidate after a successful attach. Release only source
        // capabilities; project-root roles remain owned by EditorFileAccessContext.
        candidateContext = null;
        completed = true;
        try
        {
            plan.Selection.DisposeSourceCapabilities();
        }
        catch
        {
            // The editor already owns the candidate. Cleanup failures must never turn a
            // committed project into a rollback request.
        }
        finally
        {
            try
            {
                plan.Dispose();
            }
            catch
            {
                // Preserve the same commit boundary for best-effort source cleanup.
            }
            ReleaseIoLease();
        }
    }

    public async Task<IReadOnlyList<string>> RollbackAsync(
        IProgress<EditorProjectCreationProgress>? progress = null)
    {
        if (completed)
            return [];

        Report(progress, EditorProjectCreationPhase.RollingBack);
        var failures = new List<string>();
        for (var index = createdFiles.Count - 1; index >= 0; index--)
        {
            var file = createdFiles[index];
            try
            {
                await file.DeleteAsync(CancellationToken.None);
            }
            catch (Exception exception)
            {
                failures.Add($"{file.FileName}: {exception.Message}");
            }
        }

        TryDisposeCandidate(candidateContext, "Candidate context", failures);
        TryDisposeCandidate(candidateFileContext, "Candidate file handles", failures);
        candidateContext = null;
        candidateFileContext = null;

        try
        {
            if (projectRootTransferred)
                plan.Selection.DisposeSourceCapabilities();
            plan.Dispose();
        }
        catch (Exception exception)
        {
            failures.Add($"Source handles: {exception.Message}");
        }

        completed = true;
        ReleaseIoLease();
        return failures;
    }

    public void Dispose()
    {
        if (disposed)
            return;
        disposed = true;
        if (!completed)
        {
            // Synchronous disposal is a last-resort ownership cleanup. Coordinators always
            // call RollbackAsync first so deletion remains observable and best-effort.
            try
            {
                candidateContext?.Dispose();
            }
            catch
            {
                // A last-resort Dispose must still release the source plan and gate.
            }
            try
            {
                candidateFileContext?.Dispose();
            }
            catch
            {
                // A last-resort Dispose must still release the source plan and gate.
            }
            candidateContext = null;
            candidateFileContext = null;
            try
            {
                if (projectRootTransferred)
                    plan.Selection.DisposeSourceCapabilities();
            }
            catch
            {
                // Continue releasing the frozen plan even when a source capability fails.
            }
            try
            {
                plan.Dispose();
            }
            catch
            {
                // Dispose is best effort during last-resort shutdown.
            }
        }
        ReleaseIoLease();
    }

    private void ValidateFrozenSelection(EditorProjectSetupSelection selection)
    {
        if (string.IsNullOrWhiteSpace(selection.ProjectName))
            throw InvalidSelection("A project name is required.");
        PortableEntryNameValidator.ThrowIfInvalid(selection.ProjectFileName, nameof(selection.ProjectFileName));
        var audioTargetName = selection.TargetAudioFileName ?? selection.AudioFile.FileName;
        if (!Path.GetExtension(audioTargetName).Equals(
                Path.GetExtension(selection.AudioFile.FileName),
                StringComparison.OrdinalIgnoreCase))
        {
            throw InvalidSelection("The audio target must keep the source audio extension.");
        }
        if (selection.FumenMode == SetupFumenMode.CreateNew)
        {
            if (selection.BaseBpm is not { } bpm || !double.IsFinite(bpm) || bpm <= 0)
                throw InvalidSelection("The initial BPM must be a finite number greater than zero.");
            if (selection.NewFumenFileName is null)
                throw InvalidSelection("A new fumen file name is required.");
        }
        else if (selection.ExistingFumenFile is null)
        {
            throw InvalidSelection("An existing fumen is required.");
        }
        else
        {
            var targetName = selection.ExistingFumenTargetFileName ??
                selection.ExistingFumenFile.FileName;
            if (!Path.GetExtension(targetName).Equals(
                    Path.GetExtension(selection.ExistingFumenFile.FileName),
                    StringComparison.OrdinalIgnoreCase))
            {
                throw InvalidSelection("The existing fumen target must keep its source extension.");
            }
        }

        foreach (var name in plan.PlannedTargetFileNames)
        {
            var result = PortableEntryNameValidator.Validate(name);
            if (!result.IsValid)
                throw InvalidSelection($"The target name '{name}' is invalid ({result.Error}).");
        }
    }

    private static void ValidateAudioPackage(
        EditorProjectSetupSelection selection,
        AcbPackageInspection inspection)
    {
        if (!inspection.IsValid)
        {
            throw new EditorProjectCreationException(
                EditorProjectCreationFailureKind.InvalidAudioPackage,
                inspection.ErrorMessage ?? "The selected audio package is invalid.");
        }

        if (inspection.Kind != selection.AudioPackageKind)
            throw InvalidAudio("The selected audio package changed after it was inspected.");
        if (inspection.Kind != SetupAudioPackageKind.AcbWithExternalAwb)
        {
            if (selection.AudioAwbFile is not null)
                throw InvalidAudio("This audio file does not require an external AWB.");
            return;
        }

        if (selection.AudioAwbFile is null)
            throw InvalidAudio("The external AWB declared by the ACB is required.");
        if (!string.Equals(
                inspection.RequiredExternalAwbLeafName,
                selection.TargetAudioAwbFileName,
                StringComparison.OrdinalIgnoreCase))
        {
            throw InvalidAudio(
                $"The external AWB target must be named '{inspection.RequiredExternalAwbLeafName}'.");
        }
    }

    private async Task<TimeSpan> ValidateAudioAsync(
        EditorProjectSetupSelection selection,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            await using var audioStream = await selection.AudioFile.OpenRead();
            IAudioPlayer player;
            if (selection.AudioAwbFile is { } externalAwbFile)
            {
                await using var externalAwbStream = await externalAwbFile.OpenRead();
                player = await audioManager.LoadAudioAsync(
                    audioStream,
                    externalAwbStream);
            }
            else
            {
                player = await audioManager.LoadAudioAsync(audioStream);
            }

            using (player)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return player.Duration;
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new EditorProjectCreationException(
                EditorProjectCreationFailureKind.InvalidAudioPackage,
                $"The selected audio cannot be decoded: {exception.Message}",
                exception);
        }
    }

    private async Task<byte[]> PrepareNewFumenAsync(
        EditorProjectSetupSelection selection,
        CancellationToken cancellationToken)
    {
        var fileName = selection.NewFumenFileName!;
        var serializer = parserManager.GetSerializer(fileName) ??
            throw UnsupportedFormat($"No fumen serializer supports '{fileName}'.");
        var deserializer = parserManager.GetDeserializer(fileName) ??
            throw UnsupportedFormat($"No fumen deserializer supports '{fileName}'.");

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var expectedBpm = selection.BaseBpm!.Value;
            var fumen = EditorProjectSetupValidation.CreateBlankFumen(expectedBpm);
            var expectedProgJudgeBpm = fumen.MetaInfo.ProgJudgeBpm;
            var bytes = await serializer.SerializeAsync(fumen);
            cancellationToken.ThrowIfCancellationRequested();
            await using var verificationStream = new MemoryStream(bytes, writable: false);
            var verified = await deserializer.DeserializeAsync(verificationStream);
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                VerifyInitialBpm(verified, expectedBpm, expectedProgJudgeBpm, fileName);
            }
            finally
            {
                DisposeFumenResources(verified);
            }
            return bytes;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (EditorProjectCreationException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new EditorProjectCreationException(
                EditorProjectCreationFailureKind.VerificationFailed,
                $"The fumen format '{fileName}' could not round-trip the initial chart: {exception.Message}",
                exception);
        }
    }

    private async Task ValidateExistingFumenAsync(
        EditorProjectSetupSelection selection,
        CancellationToken cancellationToken)
    {
        var source = selection.ExistingFumenFile!;
        var targetName = selection.ExistingFumenTargetFileName ?? source.FileName;
        if (parserManager.GetSerializer(targetName) is null ||
            parserManager.GetDeserializer(targetName) is null)
        {
            throw UnsupportedFormat($"The target fumen format '{targetName}' is not writable and readable.");
        }

        var deserializer = parserManager.GetDeserializer(source.FileName) ??
            throw UnsupportedFormat($"No fumen deserializer supports '{source.FileName}'.");
        try
        {
            await using var stream = await source.OpenRead();
            cancellationToken.ThrowIfCancellationRequested();
            var fumen = await deserializer.DeserializeAsync(stream);
            cancellationToken.ThrowIfCancellationRequested();
            DisposeFumenResources(fumen);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (EditorProjectCreationException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new EditorProjectCreationException(
                EditorProjectCreationFailureKind.SourceReadFailed,
                $"The selected fumen cannot be read: {exception.Message}",
                exception);
        }
    }

    private async Task<byte[]> PrepareProjectDataAsync(
        TimeSpan audioDuration,
        CancellationToken cancellationToken)
    {
        var projectData = new EditorProjectDataModel
        {
            AudioDuration = audioDuration
        };
        using var stream = new MemoryStream();
        await projectFileManager.Save(stream, projectData, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        return stream.ToArray();
    }

    private async Task ValidateTargetConflictsAsync(
        ISimpleDirectory projectDirectory,
        CancellationToken cancellationToken)
    {
        var entries = await projectDirectory.GetEntrySnapshotAsync(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        var existingNames = entries
            .Select(entry => entry.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var conflicts = plan.PlannedTargetFileNames
            .Where(existingNames.Contains)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (conflicts.Length > 0)
        {
            throw new EditorProjectCreationException(
                EditorProjectCreationFailureKind.TargetConflict,
                $"The project folder already contains: {string.Join(", ", conflicts)}.");
        }
    }

    private async Task<ISimpleFile> CopyFileAsync(
        ISimpleDirectory root,
        EditorProjectFileCopyPlan copy,
        EditorProjectCreationPhase phase,
        IProgress<EditorProjectCreationProgress>? progress,
        CancellationToken cancellationToken)
    {
        var target = await CreateTrackedFileAsync(root, copy.TargetFileName, cancellationToken);
        try
        {
            long? totalBytes = copy.SourceFile.FileLength > 0 ? copy.SourceFile.FileLength : null;
            Report(progress, phase, copy.TargetFileName, 0, totalBytes);
            await target.WriteAsync(async (output, writerCancellationToken) =>
            {
                await using var input = await copy.SourceFile.OpenRead();
                writerCancellationToken.ThrowIfCancellationRequested();
                var rented = ArrayPool<byte>.Shared.Rent(81_920);
                long copied = 0;
                try
                {
                    while (true)
                    {
                        var count = await input.ReadAsync(rented.AsMemory(0, rented.Length), writerCancellationToken);
                        if (count == 0)
                            break;
                        await output.WriteAsync(rented.AsMemory(0, count), writerCancellationToken);
                        copied += count;
                        Report(progress, phase, copy.TargetFileName, copied, totalBytes);
                    }
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(rented);
                }
            }, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            return target;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new EditorProjectCreationException(
                EditorProjectCreationFailureKind.TargetWriteFailed,
                $"Unable to copy '{copy.SourceFile.FileName}': {exception.Message}",
                exception);
        }
    }

    private async Task<ISimpleFile> CreateAndWriteAsync(
        ISimpleDirectory root,
        string fileName,
        byte[] bytes,
        EditorProjectCreationPhase phase,
        IProgress<EditorProjectCreationProgress>? progress,
        CancellationToken cancellationToken)
    {
        var target = await CreateTrackedFileAsync(root, fileName, cancellationToken);
        try
        {
            Report(progress, phase, fileName, 0, bytes.LongLength);
            await target.WriteAsync(async (stream, writerCancellationToken) =>
            {
                await stream.WriteAsync(bytes, writerCancellationToken);
            }, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            Report(progress, phase, fileName, bytes.LongLength, bytes.LongLength);
            return target;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new EditorProjectCreationException(
                EditorProjectCreationFailureKind.TargetWriteFailed,
                $"Unable to write '{fileName}': {exception.Message}",
                exception);
        }
    }

    private async Task<ISimpleFile> CreateTrackedFileAsync(
        ISimpleDirectory root,
        string fileName,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var file = await root.CreateFileAsync(fileName, cancellationToken);
        createdFiles.Add(file);
        cancellationToken.ThrowIfCancellationRequested();
        return file;
    }

    private static IEnumerable<EditorProjectFileCopyPlan> OrderCopyPlans(
        IReadOnlyList<EditorProjectFileCopyPlan> plans) =>
        plans.OrderBy(plan => plan.Role switch
        {
            EditorProjectFileRole.AudioAwb => 0,
            EditorProjectFileRole.Audio => 1,
            EditorProjectFileRole.Fumen => 2,
            _ => 3
        });

    private static void EnsureRequiredRoles(
        IReadOnlyDictionary<EditorProjectFileRole, ISimpleFile> files)
    {
        foreach (var role in new[]
                 {
                     EditorProjectFileRole.Project,
                     EditorProjectFileRole.Fumen,
                     EditorProjectFileRole.Audio
                 })
        {
            if (!files.ContainsKey(role))
            {
                throw new EditorProjectCreationException(
                    EditorProjectCreationFailureKind.VerificationFailed,
                    $"The creation plan did not produce the required '{role}' file.");
            }
        }
    }

    private static string BuildLocationDescription(EditorProjectSetupSelection selection) =>
        string.IsNullOrWhiteSpace(selection.ProjectDirectoryDisplayName)
            ? selection.ProjectFileName
            : $"{selection.ProjectDirectoryDisplayName}/{selection.ProjectFileName}";

    private static void DisposeFumenResources(OngekiFumen fumen)
    {
        foreach (var svg in fumen.SvgPrefabs.ToArray())
            svg.Dispose();
    }

    private static void VerifyInitialBpm(
        OngekiFumen fumen,
        double expectedBpm,
        float expectedProgJudgeBpm,
        string fileName)
    {
        var definition = fumen.MetaInfo.BpmDefinition;
        if (definition.First != expectedBpm ||
            definition.Common != expectedBpm ||
            definition.Minimum != expectedBpm ||
            definition.Maximum != expectedBpm ||
            fumen.BpmList.FirstBpm != expectedBpm ||
            fumen.MetaInfo.ProgJudgeBpm != expectedProgJudgeBpm)
        {
            throw new EditorProjectCreationException(
                EditorProjectCreationFailureKind.VerificationFailed,
                $"The fumen format '{fileName}' did not preserve the initial BPM values.");
        }
    }

    private static EditorProjectCreationException InvalidSelection(string message) =>
        new(EditorProjectCreationFailureKind.InvalidSelection, message);

    private static EditorProjectCreationException InvalidAudio(string message) =>
        new(EditorProjectCreationFailureKind.InvalidAudioPackage, message);

    private static EditorProjectCreationException UnsupportedFormat(string message) =>
        new(EditorProjectCreationFailureKind.UnsupportedFormat, message);

    private static void Report(
        IProgress<EditorProjectCreationProgress>? progress,
        EditorProjectCreationPhase phase,
        string? fileName = null,
        long completed = 0,
        long? total = null) =>
        progress?.Report(new EditorProjectCreationProgress(
            phase,
            fileName,
            completed,
            total is > 0 ? total : null));

    private void ReleaseIoLease()
    {
        Interlocked.Exchange(ref ioLease, null)?.Dispose();
    }

    private static void TryDisposeCandidate(
        IDisposable? candidate,
        string label,
        ICollection<string> failures)
    {
        if (candidate is null)
            return;

        try
        {
            candidate.Dispose();
        }
        catch (Exception exception)
        {
            failures.Add($"{label}: {exception.Message}");
        }
    }
}

public sealed class EditorProjectCreationCoordinator
{
    private readonly IFumenParserManager parserManager;
    private readonly IAudioManager audioManager;
    private readonly Func<EditorContext, CancellationToken, Task<bool>> attachEditorAsync;

    public EditorProjectCreationCoordinator(
        IFumenParserManager parserManager,
        IAudioManager audioManager,
        Func<EditorContext, CancellationToken, Task<bool>> attachEditorAsync)
    {
        this.parserManager = parserManager ?? throw new ArgumentNullException(nameof(parserManager));
        this.audioManager = audioManager ?? throw new ArgumentNullException(nameof(audioManager));
        this.attachEditorAsync = attachEditorAsync ?? throw new ArgumentNullException(nameof(attachEditorAsync));
    }

    public async Task<EditorProjectCreationOutcome> RunAsync(
        EditorProjectCreationPlan plan,
        IProgress<EditorProjectCreationProgress>? progress,
        CancellationToken cancellationToken,
        Func<bool>? tryBeginFinalizing = null,
        Action? beginRollback = null)
    {
        using var transaction = new EditorProjectCreationTransaction(
            plan,
            parserManager,
            audioManager);
        try
        {
            var candidate = await transaction.PrepareAsync(progress, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (tryBeginFinalizing is not null && !tryBeginFinalizing())
            {
                beginRollback?.Invoke();
                var rollbackFailures = await transaction.RollbackAsync(progress);
                return new EditorProjectCreationOutcome.Canceled(rollbackFailures);
            }

            progress?.Report(new EditorProjectCreationProgress(
                EditorProjectCreationPhase.AttachingEditor));
            bool attached;
            try
            {
                // Finalizing is the commit boundary. A late user cancellation cannot tear down
                // files after the editor has accepted their owning context.
                attached = await attachEditorAsync(candidate, CancellationToken.None);
            }
            catch (Exception exception)
            {
                throw new EditorProjectCreationException(
                    EditorProjectCreationFailureKind.EditorRejected,
                    $"The editor could not open the new project: {exception.Message}",
                    exception);
            }

            if (!attached)
            {
                throw new EditorProjectCreationException(
                    EditorProjectCreationFailureKind.EditorRejected,
                    "The editor rejected the new project.");
            }

            transaction.Commit();
            return new EditorProjectCreationOutcome.Succeeded();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            beginRollback?.Invoke();
            var rollbackFailures = await transaction.RollbackAsync(progress);
            return new EditorProjectCreationOutcome.Canceled(rollbackFailures);
        }
        catch (Exception exception)
        {
            beginRollback?.Invoke();
            var rollbackFailures = await transaction.RollbackAsync(progress);
            var kind = exception is EditorProjectCreationException creationException
                ? creationException.Kind
                : EditorProjectCreationFailureKind.Unknown;
            return new EditorProjectCreationOutcome.Failed(kind, exception, rollbackFailures);
        }
    }
}
