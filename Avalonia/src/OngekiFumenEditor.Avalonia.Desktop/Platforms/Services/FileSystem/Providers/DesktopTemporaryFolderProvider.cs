#nullable enable

using Injectio.Attributes;

namespace OngekiFumenEditor.Avalonia.Platforms.Services.FileSystem.Providers;

[RegisterSingleton<ITemporaryFolderProvider>]
public sealed class DesktopTemporaryFolderProvider : TemporaryFolderProviderBase
{
    public const string RootFolderName = "NagekiFumenEditorTempFolder";
    private const int BufferSize = 81_920;
    private readonly string rootPath;

    public DesktopTemporaryFolderProvider()
        : this(DefaultRootPath)
    {
    }

    internal DesktopTemporaryFolderProvider(string rootPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
        this.rootPath = Path.GetFullPath(rootPath);
    }

    public static string DefaultRootPath => Path.Combine(Path.GetTempPath(), RootFolderName);

    public override bool IsAvailable => true;

    internal string RootPath => rootPath;

    protected override string GetLocalPathCore(string relativePath) => GetContainedPath(relativePath);

    protected override Task<TemporaryEntryKind> GetEntryKindCoreAsync(
        string relativePath,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string path = GetContainedPath(relativePath);
        TemporaryEntryKind kind = File.Exists(path)
            ? TemporaryEntryKind.File
            : Directory.Exists(path)
                ? TemporaryEntryKind.Folder
                : TemporaryEntryKind.Missing;
        return Task.FromResult(kind);
    }

    protected override Task CreateFileCoreAsync(string relativePath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string path = GetContainedPath(relativePath);
        EnsureParentDirectory(path);
        if (Directory.Exists(path))
            throw new IOException($"A folder already exists at temporary path '{relativePath}'.");

        using var stream = new FileStream(
            path,
            FileMode.OpenOrCreate,
            FileAccess.Write,
            FileShare.Read,
            1,
            FileOptions.Asynchronous);
        return Task.CompletedTask;
    }

    protected override Task<bool> TryCreateFileCoreAsync(
        string relativePath,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string path = GetContainedPath(relativePath);
        EnsureParentDirectory(path);

        try
        {
            using var stream = new FileStream(
                path,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.Read,
                1,
                FileOptions.Asynchronous);
            return Task.FromResult(true);
        }
        catch (IOException) when (File.Exists(path) || Directory.Exists(path))
        {
            return Task.FromResult(false);
        }
    }

    protected override Task CreateFolderCoreAsync(string relativePath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string path = GetContainedPath(relativePath);
        if (File.Exists(path))
            throw new IOException($"A file already exists at temporary path '{relativePath}'.");

        Directory.CreateDirectory(path);
        return Task.CompletedTask;
    }

    protected override Task<bool> TryCreateFolderCoreAsync(
        string relativePath,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string path = GetContainedPath(relativePath);
        if (File.Exists(path) || Directory.Exists(path))
            return Task.FromResult(false);

        Directory.CreateDirectory(path);
        return Task.FromResult(true);
    }

    protected override Task<long> GetFileLengthCoreAsync(
        string relativePath,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string path = GetContainedPath(relativePath);
        if (!File.Exists(path))
            throw new FileNotFoundException($"Temporary file '{relativePath}' does not exist.", path);

        return Task.FromResult(new FileInfo(path).Length);
    }

    protected override Task<byte[]> ReadAllBytesCoreAsync(
        string relativePath,
        CancellationToken cancellationToken) =>
        File.ReadAllBytesAsync(GetContainedPath(relativePath), cancellationToken);

    protected override Task<Stream> OpenReadCoreAsync(
        string relativePath,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<Stream>(new FileStream(
            GetContainedPath(relativePath),
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            BufferSize,
            FileOptions.Asynchronous | FileOptions.SequentialScan));
    }

    protected override async Task WriteFileCoreAsync(
        string relativePath,
        Func<Stream, CancellationToken, Task> writer,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string targetPath = GetContainedPath(relativePath);
        EnsureParentDirectory(targetPath);
        string temporaryPath = GetContainedPath(TemporaryEntryNameForTransaction(relativePath));

        try
        {
            await using (var stream = new FileStream(
                             temporaryPath,
                             FileMode.CreateNew,
                             FileAccess.Write,
                             FileShare.None,
                             BufferSize,
                             FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                await writer(stream, cancellationToken).ConfigureAwait(false);

                // The writer completed. Commit must now run to completion even if cancellation is requested.
                await stream.FlushAsync(CancellationToken.None).ConfigureAwait(false);
            }

            File.Move(temporaryPath, targetPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
    }

    protected override async Task AppendFileCoreAsync(
        string relativePath,
        ReadOnlyMemory<byte> data,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string path = GetContainedPath(relativePath);
        EnsureParentDirectory(path);
        await using var stream = new FileStream(
            path,
            FileMode.Append,
            FileAccess.Write,
            FileShare.Read,
            BufferSize,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        await stream.WriteAsync(data, cancellationToken).ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    protected override Task DeleteFileCoreAsync(string relativePath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string path = GetContainedPath(relativePath);
        if (Directory.Exists(path))
            throw new IOException($"A folder exists at temporary file path '{relativePath}'.");

        File.Delete(path);
        return Task.CompletedTask;
    }

    protected override Task DeleteFolderCoreAsync(string relativePath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        DeleteDirectoryWithoutFollowingLinks(GetContainedPath(relativePath));
        return Task.CompletedTask;
    }

    protected override Task ClearCoreAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Directory.CreateDirectory(rootPath);

        foreach (FileSystemInfo entry in new DirectoryInfo(rootPath).EnumerateFileSystemInfos())
        {
            cancellationToken.ThrowIfCancellationRequested();
            _ = GetContainedPath(Path.GetRelativePath(rootPath, entry.FullName).Replace('\\', '/'));
            if (entry is DirectoryInfo directory)
                DeleteDirectoryWithoutFollowingLinks(directory.FullName);
            else
                entry.Delete();
        }

        return Task.CompletedTask;
    }

    private string GetContainedPath(string relativePath)
    {
        string candidate = relativePath.Length == 0
            ? rootPath
            : Path.GetFullPath(Path.Combine(rootPath, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        string relativeToRoot = Path.GetRelativePath(rootPath, candidate);
        if (relativeToRoot == ".." ||
            relativeToRoot.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal) ||
            Path.IsPathRooted(relativeToRoot))
        {
            throw new IOException($"Temporary path '{relativePath}' escapes the configured root.");
        }

        return candidate;
    }

    private static void EnsureParentDirectory(string filePath)
    {
        string directoryPath = Path.GetDirectoryName(filePath)
            ?? throw new IOException($"Temporary file '{filePath}' has no parent directory.");
        Directory.CreateDirectory(directoryPath);
    }

    private static void DeleteDirectoryWithoutFollowingLinks(string path)
    {
        var directory = new DirectoryInfo(path);
        if (!directory.Exists)
            return;

        if ((directory.Attributes & FileAttributes.ReparsePoint) != 0)
            directory.Delete();
        else
            directory.Delete(recursive: true);
    }

    private static string TemporaryEntryNameForTransaction(string relativePath)
    {
        int separator = relativePath.LastIndexOf('/');
        string parent = separator < 0 ? string.Empty : relativePath[..separator];
        string name = separator < 0 ? relativePath : relativePath[(separator + 1)..];
        string transactionName = $".{name}.{Guid.NewGuid():N}.tmp";
        return parent.Length == 0 ? transactionName : $"{parent}/{transactionName}";
    }
}
