#nullable enable

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Avalonia.Utils.SimpleFileSystem;

internal static class SimpleFileWriteTransaction
{
    private const int BufferSize = 81_920;

    public static async Task WriteAsync(
        ISimpleFile file,
        Func<Stream, CancellationToken, Task> writer,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(writer);

        cancellationToken.ThrowIfCancellationRequested();
        await using var stream = await file.OpenWrite().ConfigureAwait(false);
        await writer(stream, cancellationToken).ConfigureAwait(false);
        await stream.FlushAsync(CancellationToken.None).ConfigureAwait(false);
    }

    public static async Task<long> WriteLocalAsync(
        string localPath,
        Func<Stream, CancellationToken, Task> writer,
        CancellationToken cancellationToken,
        Func<string, long, Task>? accessDeniedCommitFallback = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(localPath);
        ArgumentNullException.ThrowIfNull(writer);
        cancellationToken.ThrowIfCancellationRequested();

        var targetPath = Path.GetFullPath(localPath);
        var directoryPath = Path.GetDirectoryName(targetPath)
            ?? throw new ArgumentException("The file path must include a parent directory.", nameof(localPath));
        var temporaryPath = Path.Combine(
            directoryPath,
            $".{Path.GetFileName(targetPath)}.{Guid.NewGuid():N}.tmp");

        try
        {
            long fileLength;
            await using (var stream = new FileStream(
                             temporaryPath,
                             FileMode.CreateNew,
                             FileAccess.Write,
                             FileShare.None,
                             BufferSize,
                             FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                await writer(stream, cancellationToken).ConfigureAwait(false);

                // Once the writer succeeds, cancellation must not leave a completed replacement uncommitted.
                await stream.FlushAsync(CancellationToken.None).ConfigureAwait(false);
                fileLength = stream.Length;
            }

            try
            {
                File.Move(temporaryPath, targetPath, overwrite: true);
            }
            catch (UnauthorizedAccessException) when (accessDeniedCommitFallback is not null)
            {
                await accessDeniedCommitFallback(temporaryPath, fileLength).ConfigureAwait(false);
            }

            return fileLength;
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
    }
}
