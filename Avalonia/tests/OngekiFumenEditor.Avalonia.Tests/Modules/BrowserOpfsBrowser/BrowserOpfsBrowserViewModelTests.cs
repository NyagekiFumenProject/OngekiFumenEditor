#nullable enable

using System.Collections.Specialized;
using Gekimini.Avalonia.Framework.Dialogs;
using Gekimini.Avalonia.Modules.Dialogs.ViewModels;
using OngekiFumenEditor.Avalonia.Browser.Modules.BrowserOpfsBrowser;
using OngekiFumenEditor.Avalonia.Browser.Modules.BrowserOpfsBrowser.Assets.Languages;
using OngekiFumenEditor.Avalonia.Browser.Modules.BrowserOpfsBrowser.ViewModels;
using Xunit;

namespace OngekiFumenEditor.Avalonia.Tests.Modules.BrowserOpfsBrowser;

public sealed class BrowserOpfsBrowserViewModelTests
{
    static BrowserOpfsBrowserViewModelTests()
    {
        // Plain unit tests do not create the Avalonia TestApplication, while the view model
        // logs through the application-wide facade.
        OngekiFumenEditor.Avalonia.Utils.Log.Initialize(new OngekiFumenEditor.Avalonia.Utils.Log([]));
    }

    [Fact]
    public async Task RefreshNowAsync_AppliesIncrementalChangesWithoutResetAndPreservesExistingRowState()
    {
        var service = new StubBrowserOpfsService();
        service.SetDirectory(
            string.Empty,
            Folder("folder10"),
            File("file2.bin", 20, 200),
            File("file10.bin", 100, 100));
        var viewModel = CreateViewModel(service);
        await viewModel.RefreshNowAsync();

        var collection = viewModel.Entries;
        BrowserOpfsEntryViewModel existingFile = Assert.Single(collection, x => x.Name == "file2.bin");
        existingFile.IsSelected = true;
        var actions = new List<NotifyCollectionChangedAction>();
        collection.CollectionChanged += (_, args) => actions.Add(args.Action);

        service.SetDirectory(
            string.Empty,
            Folder("folder10"),
            File("file1.bin", 10, 300),
            File("file2.bin", 25, 400));
        await viewModel.RefreshNowAsync();

        Assert.Same(collection, viewModel.Entries);
        Assert.Same(existingFile, Assert.Single(viewModel.Entries, x => x.Name == "file2.bin"));
        Assert.True(existingFile.IsSelected);
        Assert.Equal(25, existingFile.Size);
        Assert.Equal(400, existingFile.LastModifiedUnixMilliseconds);
        Assert.False(Assert.Single(viewModel.Entries, x => x.Name == "file1.bin").IsSelected);
        Assert.Equal(["folder10", "file1.bin", "file2.bin"], viewModel.Entries.Select(x => x.Name));
        Assert.Contains(NotifyCollectionChangedAction.Add, actions);
        Assert.Contains(NotifyCollectionChangedAction.Remove, actions);
        Assert.Contains(NotifyCollectionChangedAction.Move, actions);
        Assert.DoesNotContain(NotifyCollectionChangedAction.Reset, actions);
        Assert.DoesNotContain(NotifyCollectionChangedAction.Replace, actions);
        Assert.Equal(1, viewModel.SelectedCount);
    }

    [Fact]
    public async Task RefreshNowAsync_WhenEntryKindChanges_ReplacesOnlyThatRowWithoutReset()
    {
        var service = new StubBrowserOpfsService();
        service.SetDirectory(string.Empty, File("switched", 10, 20));
        var viewModel = CreateViewModel(service);
        await viewModel.RefreshNowAsync();

        BrowserOpfsEntryViewModel original = Assert.Single(viewModel.Entries);
        original.IsSelected = true;
        var actions = new List<NotifyCollectionChangedAction>();
        viewModel.Entries.CollectionChanged += (_, args) => actions.Add(args.Action);

        service.SetDirectory(string.Empty, Folder("switched"));
        await viewModel.RefreshNowAsync();

        BrowserOpfsEntryViewModel replacement = Assert.Single(viewModel.Entries);
        Assert.NotSame(original, replacement);
        Assert.True(replacement.IsFolder);
        Assert.False(replacement.IsSelected);
        Assert.Equal(0, viewModel.SelectedCount);
        Assert.Equal(
            [NotifyCollectionChangedAction.Remove, NotifyCollectionChangedAction.Add],
            actions);
        Assert.DoesNotContain(NotifyCollectionChangedAction.Reset, actions);
    }

    [Fact]
    public async Task RefreshNowAsync_ListsOnlyCurrentDirectoryAndExpandedTreeDirectories()
    {
        var service = new StubBrowserOpfsService();
        service.SetDirectory(string.Empty, Folder("A"), Folder("B"));
        service.SetDirectory("A", Folder("A/A1"));
        service.SetDirectory("B", Folder("B/B1"));
        var viewModel = CreateViewModel(service);
        await viewModel.RefreshNowAsync();

        BrowserOpfsDirectoryNodeViewModel nodeA = Assert.Single(
            viewModel.RootNode.Children,
            x => x.RelativePath == "A");
        nodeA.IsExpanded = true;
        service.ListCalls.Clear();

        await viewModel.RefreshNowAsync();

        Assert.Equal([string.Empty, "A"], service.ListCalls);
        Assert.DoesNotContain("B", service.ListCalls);
    }

    [Fact]
    public async Task RefreshNowAsync_WhenCurrentFolderDisappears_ReturnsToNearestAncestorAndClearsHistory()
    {
        var service = new StubBrowserOpfsService();
        service.SetDirectory(string.Empty, Folder("A"));
        service.SetDirectory("A", Folder("A/B"));
        service.SetDirectory("A/B", File("selected.dat", 4, 10));
        var viewModel = CreateViewModel(service);
        await viewModel.RefreshNowAsync();

        BrowserOpfsEntryViewModel folderA = Assert.Single(viewModel.Entries);
        Assert.True(folderA.IsFolder);
        Assert.True(folderA.IsSelectable);
        await viewModel.OpenEntryCommand.ExecuteAsync(folderA);
        Assert.Equal("A", viewModel.CurrentPath);

        BrowserOpfsEntryViewModel folderB = Assert.Single(viewModel.Entries);
        Assert.True(folderB.IsFolder);
        Assert.True(folderB.IsSelectable);
        await viewModel.OpenEntryCommand.ExecuteAsync(folderB);
        Assert.Equal("A/B", viewModel.CurrentPath);
        Assert.Single(viewModel.Entries).IsSelected = true;

        service.RemoveDirectory("A/B");
        service.SetDirectory("A");
        await viewModel.RefreshNowAsync();

        Assert.Equal("A", viewModel.CurrentPath);
        Assert.Equal(0, viewModel.SelectedCount);
        Assert.False(viewModel.GoBackCommand.CanExecute(null));
        Assert.False(viewModel.GoForwardCommand.CanExecute(null));
        Assert.Contains("A/B", viewModel.StatusMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OpenEntryCommand_WhenEntryIsFile_OpensPreviewWithoutStartingDownload()
    {
        var service = new StubBrowserOpfsService();
        service.SetDirectory(string.Empty, File("preview.txt", 12, 20));
        var viewModel = CreateViewModel(service);
        await viewModel.RefreshNowAsync();

        BrowserOpfsEntryViewModel file = Assert.Single(viewModel.Entries);
        await viewModel.OpenEntryCommand.ExecuteAsync(file);

        Assert.Equal(["preview.txt"], service.PreviewedPaths);
        Assert.False(service.DownloadStarted.Task.IsCompleted);
        Assert.False(viewModel.IsDownloadInProgress);
    }

    [Fact]
    public async Task OpenEntryCommand_WhenPreviewPageIsBlocked_ShowsStatusMessage()
    {
        var service = new StubBrowserOpfsService { IsPreviewOpeningAllowed = false };
        service.SetDirectory(string.Empty, File("preview.txt", 12, 20));
        var viewModel = CreateViewModel(service);
        await viewModel.RefreshNowAsync();

        await viewModel.OpenEntryCommand.ExecuteAsync(Assert.Single(viewModel.Entries));

        Assert.Equal(BrowserOpfsLang.BrowserOpfsPreviewBlocked, viewModel.StatusMessage);
    }

    [Fact]
    public async Task RequestCloseAsync_DuringDownload_CancelsAndWaitsForCleanup()
    {
        var service = new StubBrowserOpfsService { BlockDownloads = true };
        service.SetDirectory(string.Empty, File("large.bin", 1024, 10));
        var dialogManager = new StubDialogManager(confirmResult: true);
        var viewModel = CreateViewModel(service, dialogManager);
        await viewModel.RefreshNowAsync();
        Assert.Single(viewModel.Entries).IsSelected = true;

        Task downloadCommand = viewModel.DownloadCommand.ExecuteAsync(null);
        await service.DownloadStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        bool shouldClose = await viewModel.RequestCloseAsync();
        await downloadCommand;

        Assert.True(shouldClose);
        Assert.True(service.CancellationObserved);
        Assert.False(viewModel.IsDownloadInProgress);
        Assert.Equal(1, dialogManager.ConfirmCallCount);
    }

    private static BrowserOpfsBrowserViewModel CreateViewModel(
        StubBrowserOpfsService service,
        StubDialogManager? dialogManager = null) =>
        new(service, dialogManager ?? new StubDialogManager(true), TimeSpan.FromHours(1));

    private static BrowserOpfsEntrySnapshot File(
        string relativePath,
        long size,
        long lastModified) =>
        new(
            Path.GetFileName(relativePath),
            relativePath,
            BrowserOpfsEntryKind.File,
            size,
            lastModified);

    private static BrowserOpfsEntrySnapshot Folder(string relativePath) =>
        new(
            Path.GetFileName(relativePath),
            relativePath,
            BrowserOpfsEntryKind.Folder,
            null,
            null);

    private sealed class StubBrowserOpfsService : IBrowserOpfsService
    {
        private readonly Dictionary<string, IReadOnlyList<BrowserOpfsEntrySnapshot>> directories =
            new(StringComparer.Ordinal)
            {
                [string.Empty] = []
            };

        public bool IsAvailable => true;
        public bool BlockDownloads { get; init; }
        public bool IsPreviewOpeningAllowed { get; init; } = true;
        public bool CancellationObserved { get; private set; }
        public List<string> ListCalls { get; } = [];
        public List<string> PreviewedPaths { get; } = [];
        public TaskCompletionSource DownloadStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public void SetDirectory(string relativePath, params BrowserOpfsEntrySnapshot[] entries)
        {
            directories[relativePath] = entries;
        }

        public void RemoveDirectory(string relativePath)
        {
            directories.Remove(relativePath);
        }

        public Task<IReadOnlyList<BrowserOpfsEntrySnapshot>> ListDirectoryAsync(
            string relativePath,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ListCalls.Add(relativePath);
            if (!directories.TryGetValue(relativePath, out var entries))
                throw new DirectoryNotFoundException(relativePath);
            return Task.FromResult(entries);
        }

        public Task<bool> DirectoryExistsAsync(
            string relativePath,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(directories.ContainsKey(relativePath));
        }

        public bool OpenFilePreview(string relativePath)
        {
            PreviewedPaths.Add(relativePath);
            return IsPreviewOpeningAllowed;
        }

        public async Task<BrowserOpfsDownloadResult> DownloadAsync(
            IReadOnlyList<BrowserOpfsEntrySnapshot> selectedEntries,
            IProgress<BrowserOpfsDownloadProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            DownloadStarted.TrySetResult();
            if (!BlockDownloads)
                return new BrowserOpfsDownloadResult(false);

            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                return new BrowserOpfsDownloadResult(false);
            }
            catch (OperationCanceledException)
            {
                CancellationObserved = true;
                throw;
            }
        }
    }

    private sealed class StubDialogManager(bool confirmResult) : IDialogManager
    {
        public int ConfirmCallCount { get; private set; }

        public Task<T> ShowDialog<T>() where T : DialogViewModelBase =>
            Task.FromException<T>(new NotSupportedException());

        public Task ShowDialog(DialogViewModelBase dialogViewModel) =>
            Task.FromException(new NotSupportedException());

        public Task ShowMessageDialog(string content, DialogMessageType messageType = DialogMessageType.Info) =>
            Task.FromException(new NotSupportedException());

        public Task<bool> ShowComfirmDialog(
            string content,
            string? title = null,
            string? yesButtonContent = null,
            string? noButtonContent = null)
        {
            ConfirmCallCount++;
            return Task.FromResult(confirmResult);
        }
    }
}
