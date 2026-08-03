#nullable enable

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

        if (file.LocalPath is { } localPath)
        {
            await WriteLocalAsync(localPath, writer, cancellationToken).ConfigureAwait(false);
            return;
        }

        cancellationToken.ThrowIfCancellationRequested();
        await using var stream = await file.OpenWrite().ConfigureAwait(false);
        await writer(stream, cancellationToken).ConfigureAwait(false);
        await stream.FlushAsync(CancellationToken.None).ConfigureAwait(false);
    }

    public static async Task<long> WriteLocalAsync(
        string localPath,
        Func<Stream, CancellationToken, Task> writer,
        CancellationToken cancellationToken)
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

            File.Move(temporaryPath, targetPath, overwrite: true);
            return fileLength;
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
    }
}
