#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Avalonia.Browser.Modules.BrowserOpfsBrowser;

public enum BrowserOpfsEntryKind
{
    File = 1,
    Folder = 2
}

public enum BrowserOpfsStagingState
{
    None = 0,
    GeneratingDownload = 1,
    WaitingAutomaticCleanup = 2
}

public sealed record BrowserOpfsEntrySnapshot(
    string Name,
    string RelativePath,
    BrowserOpfsEntryKind Kind,
    long? Size,
    long? LastModifiedUnixMilliseconds,
    BrowserOpfsStagingState StagingState = BrowserOpfsStagingState.None);

public sealed record BrowserOpfsDownloadProgress(
    string CurrentPath,
    long CompletedBytes,
    long TotalBytes,
    int CompletedFiles,
    int TotalFiles);

public sealed record BrowserOpfsDownloadResult(bool WasCanceled);

public interface IBrowserOpfsService
{
    bool IsAvailable { get; }

    Task<IReadOnlyList<BrowserOpfsEntrySnapshot>> ListDirectoryAsync(
        string relativePath,
        CancellationToken cancellationToken = default);

    Task<bool> DirectoryExistsAsync(
        string relativePath,
        CancellationToken cancellationToken = default);

    Task<BrowserOpfsDownloadResult> DownloadAsync(
        IReadOnlyList<BrowserOpfsEntrySnapshot> selectedEntries,
        IProgress<BrowserOpfsDownloadProgress>? progress = null,
        CancellationToken cancellationToken = default);
}
