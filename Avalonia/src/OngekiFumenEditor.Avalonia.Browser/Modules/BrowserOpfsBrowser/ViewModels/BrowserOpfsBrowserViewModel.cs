#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using Gekimini.Avalonia.Framework.Dialogs;
using Gekimini.Avalonia.Modules.Window.ViewModels;
using OngekiFumenEditor.Avalonia.Browser.Modules.BrowserOpfsBrowser.Assets.Languages;
using OngekiFumenEditor.Avalonia.Utils;

namespace OngekiFumenEditor.Avalonia.Browser.Modules.BrowserOpfsBrowser.ViewModels;

public enum BrowserOpfsSortColumn
{
    Name,
    Type,
    Size,
    ModifiedTime
}

public partial class BrowserOpfsBrowserViewModel : WindowViewModelBase
{
    private static readonly TimeSpan DefaultPollInterval = TimeSpan.FromSeconds(5);

    private readonly IBrowserOpfsService service;
    private readonly IDialogManager dialogManager;
    private readonly TimeSpan pollInterval;
    private readonly SemaphoreSlim refreshGate = new(1, 1);
    private readonly Stack<string> backHistory = new();
    private readonly Stack<string> forwardHistory = new();
    private CancellationTokenSource? pollingCancellationTokenSource;
    private CancellationTokenSource? downloadCancellationTokenSource;
    private Task? downloadTask;
    private bool suppressTreeNavigation;
    private bool isWindowOpen;
    private string currentPath = string.Empty;
    private BrowserOpfsDirectoryNodeViewModel? selectedTreeNode;
    private int selectedCount;
    private bool isDownloadInProgress;
    private double downloadProgressValue;
    private bool isDownloadProgressIndeterminate;
    private string downloadProgressText = string.Empty;
    private string statusMessage = string.Empty;
    private BrowserOpfsSortColumn sortColumn = BrowserOpfsSortColumn.Name;
    private bool sortAscending = true;

    public BrowserOpfsBrowserViewModel(
        IBrowserOpfsService service,
        IDialogManager dialogManager)
        : this(service, dialogManager, DefaultPollInterval)
    {
    }

    internal BrowserOpfsBrowserViewModel(
        IBrowserOpfsService service,
        IDialogManager dialogManager,
        TimeSpan pollInterval)
    {
        this.service = service ?? throw new ArgumentNullException(nameof(service));
        this.dialogManager = dialogManager ?? throw new ArgumentNullException(nameof(dialogManager));
        this.pollInterval = pollInterval;

        RootNode = BrowserOpfsDirectoryNodeViewModel.CreateRoot(this);
        RootNodes.Add(RootNode);
        selectedTreeNode = RootNode;
        UpdateBreadcrumbs();
    }

    public ObservableCollection<BrowserOpfsEntryViewModel> Entries { get; } = [];
    public ObservableCollection<BrowserOpfsDirectoryNodeViewModel> RootNodes { get; } = [];
    public ObservableCollection<BrowserOpfsBreadcrumbViewModel> Breadcrumbs { get; } = [];
    public BrowserOpfsDirectoryNodeViewModel RootNode { get; }

    public string CurrentPath
    {
        get => currentPath;
        private set
        {
            if (!SetProperty(ref currentPath, value))
                return;
            OnPropertyChanged(nameof(CurrentPathDisplay));
        }
    }

    public string CurrentPathDisplay => string.IsNullOrEmpty(CurrentPath) ? "OPFS" : $"OPFS/{CurrentPath}";

    public BrowserOpfsDirectoryNodeViewModel? SelectedTreeNode
    {
        get => selectedTreeNode;
        set
        {
            if (!SetProperty(ref selectedTreeNode, value) || suppressTreeNavigation || value is null ||
                value.IsPlaceholder)
                return;

            _ = RunUiOperationAsync(() => NavigateAsync(value.RelativePath));
        }
    }

    public int SelectedCount
    {
        get => selectedCount;
        private set
        {
            if (!SetProperty(ref selectedCount, value))
                return;
            OnPropertyChanged(nameof(ItemStatusDisplay));
            OnPropertyChanged(nameof(AreAllCurrentEntriesSelected));
            DownloadCommand.NotifyCanExecuteChanged();
        }
    }

    public int TotalCount => Entries.Count;

    public string ItemStatusDisplay => string.Format(
        CultureInfo.CurrentCulture,
        BrowserOpfsLang.BrowserOpfsItemStatus,
        TotalCount,
        SelectedCount);

    public bool? AreAllCurrentEntriesSelected
    {
        get
        {
            int selectableCount = Entries.Count(x => x.IsSelectable);
            if (selectableCount == 0 || SelectedCount == 0)
                return false;
            return SelectedCount == selectableCount ? true : null;
        }
    }

    public bool IsDownloadInProgress
    {
        get => isDownloadInProgress;
        private set
        {
            if (!SetProperty(ref isDownloadInProgress, value))
                return;
            DownloadCommand.NotifyCanExecuteChanged();
            CancelDownloadCommand.NotifyCanExecuteChanged();
        }
    }

    public double DownloadProgressValue
    {
        get => downloadProgressValue;
        private set => SetProperty(ref downloadProgressValue, value);
    }

    public bool IsDownloadProgressIndeterminate
    {
        get => isDownloadProgressIndeterminate;
        private set => SetProperty(ref isDownloadProgressIndeterminate, value);
    }

    public string DownloadProgressText
    {
        get => downloadProgressText;
        private set => SetProperty(ref downloadProgressText, value);
    }

    public string StatusMessage
    {
        get => statusMessage;
        private set => SetProperty(ref statusMessage, value);
    }

    public string NameSortIndicator => GetSortIndicator(BrowserOpfsSortColumn.Name);
    public string TypeSortIndicator => GetSortIndicator(BrowserOpfsSortColumn.Type);
    public string SizeSortIndicator => GetSortIndicator(BrowserOpfsSortColumn.Size);
    public string ModifiedTimeSortIndicator => GetSortIndicator(BrowserOpfsSortColumn.ModifiedTime);

    public async Task OnWindowOpenedAsync()
    {
        if (isWindowOpen)
            return;

        isWindowOpen = true;
        await RefreshNowAsync();
        StartPolling();
    }

    public void OnWindowClosed()
    {
        isWindowOpen = false;
        downloadCancellationTokenSource?.Cancel();
        pollingCancellationTokenSource?.Cancel();
        pollingCancellationTokenSource?.Dispose();
        pollingCancellationTokenSource = null;
    }

    public async Task<bool> RequestCloseAsync()
    {
        if (!IsDownloadInProgress)
            return true;

        bool shouldClose = await dialogManager.ShowComfirmDialog(
            BrowserOpfsLang.BrowserOpfsCloseDuringDownloadConfirm,
            BrowserOpfsLang.BrowserOpfsWindowTitle);
        if (!shouldClose)
            return false;

        downloadCancellationTokenSource?.Cancel();
        if (downloadTask is not null)
            await downloadTask;
        return true;
    }

    public Task RefreshNowAsync() => RefreshWithGateAsync(waitForCurrentRefresh: true);

    public void RequestExpand(BrowserOpfsDirectoryNodeViewModel node)
    {
        if (!isWindowOpen || node.IsPlaceholder)
            return;
        _ = RunUiOperationAsync(() => RefreshTreeNodeWithGateAsync(node));
    }

    public void SetSort(BrowserOpfsSortColumn column)
    {
        if (sortColumn == column)
            sortAscending = !sortAscending;
        else
        {
            sortColumn = column;
            sortAscending = true;
        }

        ReorderEntries();
        OnPropertyChanged(nameof(NameSortIndicator));
        OnPropertyChanged(nameof(TypeSortIndicator));
        OnPropertyChanged(nameof(SizeSortIndicator));
        OnPropertyChanged(nameof(ModifiedTimeSortIndicator));
    }

    public void SelectAllCurrentEntries()
    {
        foreach (var entry in Entries)
            entry.IsSelected = entry.IsSelectable;
        UpdateSelectionProperties();
    }

    [RelayCommand(CanExecute = nameof(CanGoBack))]
    private async Task GoBackAsync()
    {
        if (backHistory.Count == 0)
            return;

        string target = backHistory.Pop();
        forwardHistory.Push(CurrentPath);
        await NavigateWithoutHistoryAsync(target);
        UpdateNavigationCommandStates();
    }

    private bool CanGoBack() => backHistory.Count > 0;

    [RelayCommand(CanExecute = nameof(CanGoForward))]
    private async Task GoForwardAsync()
    {
        if (forwardHistory.Count == 0)
            return;

        string target = forwardHistory.Pop();
        backHistory.Push(CurrentPath);
        await NavigateWithoutHistoryAsync(target);
        UpdateNavigationCommandStates();
    }

    private bool CanGoForward() => forwardHistory.Count > 0;

    [RelayCommand(CanExecute = nameof(CanGoUp))]
    private Task GoUpAsync() => NavigateAsync(GetParentPath(CurrentPath));

    private bool CanGoUp() => CurrentPath.Length > 0;

    [RelayCommand]
    private Task RefreshAsync() => RefreshNowAsync();

    [RelayCommand]
    private Task NavigateBreadcrumbAsync(BrowserOpfsBreadcrumbViewModel breadcrumb) =>
        NavigateAsync(breadcrumb.RelativePath);

    [RelayCommand]
    private Task OpenEntryAsync(BrowserOpfsEntryViewModel entry)
    {
        if (entry is null || !entry.IsSelectable)
            return Task.CompletedTask;
        if (entry.IsFolder)
            return NavigateAsync(entry.RelativePath);

        StatusMessage = service.OpenFilePreview(entry.RelativePath)
            ? string.Empty
            : BrowserOpfsLang.BrowserOpfsPreviewBlocked;
        return Task.CompletedTask;
    }

    [RelayCommand]
    private void ToggleSelectAll()
    {
        bool select = AreAllCurrentEntriesSelected != true;
        foreach (var entry in Entries)
            entry.IsSelected = select && entry.IsSelectable;
        UpdateSelectionProperties();
    }

    [RelayCommand(CanExecute = nameof(CanDownload))]
    private Task DownloadAsync() => BeginDownloadAsync(Entries.Where(x => x.IsSelected && x.IsSelectable).ToArray());

    private bool CanDownload() => !IsDownloadInProgress && SelectedCount > 0;

    [RelayCommand(CanExecute = nameof(CanCancelDownload))]
    private void CancelDownload() => downloadCancellationTokenSource?.Cancel();

    private bool CanCancelDownload() => IsDownloadInProgress;

    private async Task NavigateAsync(string targetPath)
    {
        string normalizedPath = NormalizePath(targetPath);
        if (normalizedPath == CurrentPath)
        {
            await RefreshNowAsync();
            return;
        }

        backHistory.Push(CurrentPath);
        forwardHistory.Clear();
        await NavigateWithoutHistoryAsync(normalizedPath);
        UpdateNavigationCommandStates();
    }

    private async Task NavigateWithoutHistoryAsync(string targetPath)
    {
        ClearSelection();
        CurrentPath = NormalizePath(targetPath);
        UpdateBreadcrumbs();
        SelectLoadedTreeNode(CurrentPath);
        UpdateNavigationCommandStates();
        await RefreshNowAsync();
    }

    private async Task BeginDownloadAsync(IReadOnlyList<BrowserOpfsEntryViewModel> entries)
    {
        if (IsDownloadInProgress || entries.Count == 0)
            return;

        var snapshots = entries.Select(x => x.ToSnapshot()).ToArray();
        downloadCancellationTokenSource = new CancellationTokenSource();
        IsDownloadInProgress = true;
        IsDownloadProgressIndeterminate = true;
        DownloadProgressValue = 0;
        DownloadProgressText = BrowserOpfsLang.BrowserOpfsPreparingDownload;
        StatusMessage = string.Empty;

        var progress = new Progress<BrowserOpfsDownloadProgress>(UpdateDownloadProgress);
        downloadTask = RunDownloadAsync(snapshots, progress, downloadCancellationTokenSource.Token);
        await downloadTask;
    }

    private async Task RunDownloadAsync(
        IReadOnlyList<BrowserOpfsEntrySnapshot> snapshots,
        IProgress<BrowserOpfsDownloadProgress> progress,
        CancellationToken cancellationToken)
    {
        try
        {
            BrowserOpfsDownloadResult result = await service.DownloadAsync(snapshots, progress, cancellationToken);
            StatusMessage = result.WasCanceled
                ? BrowserOpfsLang.BrowserOpfsDownloadCanceled
                : BrowserOpfsLang.BrowserOpfsDownloadCompleted;
        }
        catch (OperationCanceledException)
        {
            StatusMessage = BrowserOpfsLang.BrowserOpfsDownloadCanceled;
        }
        catch (Exception exception)
        {
            Log.LogError("Browser OPFS download failed.", exception);
            StatusMessage = string.Format(
                CultureInfo.CurrentCulture,
                BrowserOpfsLang.BrowserOpfsDownloadFailed,
                exception.Message);
        }
        finally
        {
            downloadCancellationTokenSource?.Dispose();
            downloadCancellationTokenSource = null;
            downloadTask = null;
            IsDownloadInProgress = false;
            IsDownloadProgressIndeterminate = false;
            await RefreshNowAsync();
        }
    }

    private void UpdateDownloadProgress(BrowserOpfsDownloadProgress progress)
    {
        IsDownloadProgressIndeterminate = progress.TotalBytes <= 0;
        DownloadProgressValue = progress.TotalBytes <= 0
            ? 0
            : Math.Clamp(progress.CompletedBytes * 100d / progress.TotalBytes, 0, 100);
        DownloadProgressText = string.Format(
            CultureInfo.CurrentCulture,
            BrowserOpfsLang.BrowserOpfsDownloadProgress,
            progress.CompletedFiles,
            progress.TotalFiles,
            FormatByteSize(progress.CompletedBytes),
            FormatByteSize(progress.TotalBytes),
            progress.CurrentPath);
    }

    private void StartPolling()
    {
        pollingCancellationTokenSource?.Cancel();
        pollingCancellationTokenSource?.Dispose();
        pollingCancellationTokenSource = new CancellationTokenSource();
        _ = PollAsync(pollingCancellationTokenSource.Token);
    }

    private async Task PollAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var timer = new PeriodicTimer(pollInterval);
            while (await timer.WaitForNextTickAsync(cancellationToken))
                await RefreshWithGateAsync(waitForCurrentRefresh: false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            Log.LogError("Browser OPFS polling failed.", exception);
            StatusMessage = string.Format(
                CultureInfo.CurrentCulture,
                BrowserOpfsLang.BrowserOpfsRefreshFailed,
                exception.Message);
        }
    }

    private async Task RefreshWithGateAsync(bool waitForCurrentRefresh)
    {
        bool entered = waitForCurrentRefresh
            ? await refreshGate.WaitAsync(Timeout.InfiniteTimeSpan)
            : await refreshGate.WaitAsync(TimeSpan.Zero);
        if (!entered)
            return;

        try
        {
            await RefreshCoreAsync();
        }
        catch (Exception exception)
        {
            Log.LogError("Browser OPFS refresh failed.", exception);
            StatusMessage = string.Format(
                CultureInfo.CurrentCulture,
                BrowserOpfsLang.BrowserOpfsRefreshFailed,
                exception.Message);
        }
        finally
        {
            refreshGate.Release();
        }
    }

    private async Task RefreshCoreAsync()
    {
        if (!service.IsAvailable)
        {
            StatusMessage = BrowserOpfsLang.BrowserOpfsUnavailable;
            return;
        }

        if (!await service.DirectoryExistsAsync(CurrentPath))
        {
            string removedPath = CurrentPathDisplay;
            string nearestPath = await FindNearestExistingAncestorAsync(CurrentPath);
            backHistory.Clear();
            forwardHistory.Clear();
            ClearSelection();
            CurrentPath = nearestPath;
            UpdateBreadcrumbs();
            SelectLoadedTreeNode(nearestPath);
            UpdateNavigationCommandStates();
            StatusMessage = string.Format(
                CultureInfo.CurrentCulture,
                BrowserOpfsLang.BrowserOpfsFolderRemoved,
                removedPath,
                CurrentPathDisplay);
        }

        var snapshotsByPath = new Dictionary<string, IReadOnlyList<BrowserOpfsEntrySnapshot>>(StringComparer.Ordinal);
        IReadOnlyList<BrowserOpfsEntrySnapshot> currentSnapshots =
            await GetDirectorySnapshotsAsync(CurrentPath, snapshotsByPath);
        ApplyEntrySnapshots(currentSnapshots);

        if (RootNode.IsExpanded)
            await RefreshExpandedTreeNodeAsync(RootNode, snapshotsByPath);
    }

    private async Task RefreshTreeNodeWithGateAsync(BrowserOpfsDirectoryNodeViewModel node)
    {
        await refreshGate.WaitAsync();
        try
        {
            var snapshotsByPath = new Dictionary<string, IReadOnlyList<BrowserOpfsEntrySnapshot>>(StringComparer.Ordinal);
            await RefreshExpandedTreeNodeAsync(node, snapshotsByPath);
        }
        catch (Exception exception)
        {
            Log.LogError("Browser OPFS tree refresh failed.", exception);
            StatusMessage = string.Format(
                CultureInfo.CurrentCulture,
                BrowserOpfsLang.BrowserOpfsRefreshFailed,
                exception.Message);
        }
        finally
        {
            refreshGate.Release();
        }
    }

    private async Task RefreshExpandedTreeNodeAsync(
        BrowserOpfsDirectoryNodeViewModel node,
        IDictionary<string, IReadOnlyList<BrowserOpfsEntrySnapshot>> snapshotsByPath)
    {
        if (!node.IsExpanded)
            return;

        IReadOnlyList<BrowserOpfsEntrySnapshot> snapshots =
            await GetDirectorySnapshotsAsync(node.RelativePath, snapshotsByPath);
        ApplyDirectorySnapshots(node, snapshots);

        foreach (var child in node.Children.Where(x => !x.IsPlaceholder && x.IsExpanded).ToArray())
            await RefreshExpandedTreeNodeAsync(child, snapshotsByPath);
    }

    private async Task<IReadOnlyList<BrowserOpfsEntrySnapshot>> GetDirectorySnapshotsAsync(
        string relativePath,
        IDictionary<string, IReadOnlyList<BrowserOpfsEntrySnapshot>> snapshotsByPath)
    {
        if (snapshotsByPath.TryGetValue(relativePath, out var cached))
            return cached;

        IReadOnlyList<BrowserOpfsEntrySnapshot> snapshots = await service.ListDirectoryAsync(relativePath);
        snapshotsByPath.Add(relativePath, snapshots);
        return snapshots;
    }

    private void ApplyEntrySnapshots(IReadOnlyList<BrowserOpfsEntrySnapshot> snapshots)
    {
        var snapshotKeys = snapshots
            .Select(x => (x.RelativePath, x.Kind))
            .ToHashSet();
        for (int index = Entries.Count - 1; index >= 0; index--)
        {
            BrowserOpfsEntryViewModel existing = Entries[index];
            if (!snapshotKeys.Contains((existing.RelativePath, existing.Kind)))
                Entries.RemoveAt(index);
        }

        var existingByKey = Entries.ToDictionary(x => (x.RelativePath, x.Kind));
        foreach (var snapshot in snapshots)
        {
            var key = (snapshot.RelativePath, snapshot.Kind);
            if (existingByKey.TryGetValue(key, out var existing))
            {
                existing.ApplySnapshot(snapshot);
                continue;
            }

            var added = new BrowserOpfsEntryViewModel(snapshot, UpdateSelectionProperties);
            Entries.Insert(Entries.Count, added);
            existingByKey.Add(key, added);
        }

        ReorderEntries();
        UpdateSelectionProperties();
        OnPropertyChanged(nameof(TotalCount));
        OnPropertyChanged(nameof(ItemStatusDisplay));
        OnPropertyChanged(nameof(AreAllCurrentEntriesSelected));
    }

    private void ApplyDirectorySnapshots(
        BrowserOpfsDirectoryNodeViewModel parent,
        IReadOnlyList<BrowserOpfsEntrySnapshot> snapshots)
    {
        var directorySnapshots = snapshots
            .Where(x => x.Kind == BrowserOpfsEntryKind.Folder)
            .OrderBy(x => x.Name, NaturalStringComparer.Instance)
            .ToArray();
        var paths = directorySnapshots.Select(x => x.RelativePath).ToHashSet(StringComparer.Ordinal);

        for (int index = parent.Children.Count - 1; index >= 0; index--)
        {
            BrowserOpfsDirectoryNodeViewModel child = parent.Children[index];
            if (child.IsPlaceholder || !paths.Contains(child.RelativePath))
                parent.Children.RemoveAt(index);
        }

        var existingByPath = parent.Children.ToDictionary(x => x.RelativePath, StringComparer.Ordinal);
        foreach (var snapshot in directorySnapshots)
        {
            if (existingByPath.ContainsKey(snapshot.RelativePath))
                continue;
            var added = BrowserOpfsDirectoryNodeViewModel.CreateDirectory(this, snapshot.Name, snapshot.RelativePath);
            parent.Children.Insert(parent.Children.Count, added);
            existingByPath.Add(snapshot.RelativePath, added);
        }

        for (int targetIndex = 0; targetIndex < directorySnapshots.Length; targetIndex++)
        {
            string desiredPath = directorySnapshots[targetIndex].RelativePath;
            int currentIndex = IndexOfPath(parent.Children, desiredPath);
            if (currentIndex >= 0 && currentIndex != targetIndex)
                parent.Children.Move(currentIndex, targetIndex);
        }

        parent.IsLoaded = true;
    }

    private void ReorderEntries()
    {
        BrowserOpfsEntryViewModel[] desiredOrder = Entries.OrderBy(x => x, Comparer<BrowserOpfsEntryViewModel>.Create(
                CompareEntries))
            .ToArray();
        for (int targetIndex = 0; targetIndex < desiredOrder.Length; targetIndex++)
        {
            int currentIndex = Entries.IndexOf(desiredOrder[targetIndex]);
            if (currentIndex != targetIndex)
                Entries.Move(currentIndex, targetIndex);
        }
    }

    private int CompareEntries(BrowserOpfsEntryViewModel left, BrowserOpfsEntryViewModel right)
    {
        if (left.IsFolder != right.IsFolder)
            return left.IsFolder ? -1 : 1;

        int comparison = sortColumn switch
        {
            BrowserOpfsSortColumn.Type => NaturalStringComparer.Instance.Compare(left.TypeDisplay, right.TypeDisplay),
            BrowserOpfsSortColumn.Size => Nullable.Compare(left.Size, right.Size),
            BrowserOpfsSortColumn.ModifiedTime => Nullable.Compare(
                left.LastModifiedUnixMilliseconds,
                right.LastModifiedUnixMilliseconds),
            _ => NaturalStringComparer.Instance.Compare(left.Name, right.Name)
        };
        if (!sortAscending)
            comparison = -comparison;
        if (comparison != 0)
            return comparison;

        comparison = NaturalStringComparer.Instance.Compare(left.Name, right.Name);
        return comparison != 0
            ? comparison
            : string.Compare(left.RelativePath, right.RelativePath, StringComparison.Ordinal);
    }

    private async Task<string> FindNearestExistingAncestorAsync(string path)
    {
        string candidate = NormalizePath(path);
        while (candidate.Length > 0)
        {
            candidate = GetParentPath(candidate);
            if (await service.DirectoryExistsAsync(candidate))
                return candidate;
        }
        return string.Empty;
    }

    private void UpdateBreadcrumbs()
    {
        Breadcrumbs.Clear();
        Breadcrumbs.Add(new BrowserOpfsBreadcrumbViewModel("OPFS", string.Empty, true));

        string accumulatedPath = string.Empty;
        foreach (string segment in CurrentPath.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            accumulatedPath = accumulatedPath.Length == 0 ? segment : $"{accumulatedPath}/{segment}";
            Breadcrumbs.Add(new BrowserOpfsBreadcrumbViewModel(segment, accumulatedPath, false));
        }
    }

    private void SelectLoadedTreeNode(string relativePath)
    {
        BrowserOpfsDirectoryNodeViewModel? node = FindLoadedNode(RootNode, relativePath);
        if (node is null)
            return;

        suppressTreeNavigation = true;
        try
        {
            SelectedTreeNode = node;
        }
        finally
        {
            suppressTreeNavigation = false;
        }
    }

    private static BrowserOpfsDirectoryNodeViewModel? FindLoadedNode(
        BrowserOpfsDirectoryNodeViewModel node,
        string relativePath)
    {
        if (node.RelativePath == relativePath)
            return node;

        foreach (BrowserOpfsDirectoryNodeViewModel child in node.Children.Where(x => !x.IsPlaceholder))
        {
            BrowserOpfsDirectoryNodeViewModel? result = FindLoadedNode(child, relativePath);
            if (result is not null)
                return result;
        }
        return null;
    }

    private void ClearSelection()
    {
        foreach (var entry in Entries)
            entry.IsSelected = false;
        UpdateSelectionProperties();
    }

    private void UpdateSelectionProperties()
    {
        SelectedCount = Entries.Count(x => x.IsSelected && x.IsSelectable);
        OnPropertyChanged(nameof(AreAllCurrentEntriesSelected));
    }

    private void UpdateNavigationCommandStates()
    {
        GoBackCommand.NotifyCanExecuteChanged();
        GoForwardCommand.NotifyCanExecuteChanged();
        GoUpCommand.NotifyCanExecuteChanged();
    }

    private string GetSortIndicator(BrowserOpfsSortColumn column) =>
        sortColumn == column ? sortAscending ? "▲" : "▼" : string.Empty;

    private async Task RunUiOperationAsync(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (Exception exception)
        {
            Log.LogError("Browser OPFS UI operation failed.", exception);
            StatusMessage = string.Format(
                CultureInfo.CurrentCulture,
                BrowserOpfsLang.BrowserOpfsRefreshFailed,
                exception.Message);
        }
    }

    private static int IndexOfPath(
        ObservableCollection<BrowserOpfsDirectoryNodeViewModel> nodes,
        string relativePath)
    {
        for (int index = 0; index < nodes.Count; index++)
        {
            if (nodes[index].RelativePath == relativePath)
                return index;
        }
        return -1;
    }

    private static string NormalizePath(string path) =>
        (path ?? string.Empty).Replace('\\', '/').Trim('/');

    private static string GetParentPath(string path)
    {
        string normalizedPath = NormalizePath(path);
        int separatorIndex = normalizedPath.LastIndexOf('/');
        return separatorIndex < 0 ? string.Empty : normalizedPath[..separatorIndex];
    }

    private static string FormatByteSize(long byteCount)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        double value = byteCount;
        int unitIndex = 0;
        while (value >= 1024 && unitIndex < units.Length - 1)
        {
            value /= 1024;
            unitIndex++;
        }
        string format = unitIndex == 0 ? "0" : value >= 100 ? "0" : value >= 10 ? "0.0" : "0.00";
        return $"{value.ToString(format, CultureInfo.CurrentCulture)} {units[unitIndex]}";
    }
}
