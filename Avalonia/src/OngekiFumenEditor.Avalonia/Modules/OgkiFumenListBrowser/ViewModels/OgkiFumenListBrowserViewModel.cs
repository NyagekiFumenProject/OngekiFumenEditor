#nullable enable

// Injectio registration is intentionally kept on this concrete window model.

using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Gekimini.Avalonia.Framework.Dialogs;
using Gekimini.Avalonia.Framework.Languages;
using Gekimini.Avalonia.Framework.RecentFiles;
using Gekimini.Avalonia.Modules.Shell;
using Gekimini.Avalonia.Modules.Window.ViewModels;
using Gekimini.Avalonia.Utils.MethodExtensions;
using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Assets.Languages;
using OngekiFumenEditor.Avalonia.Kernel.Audio;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Models;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Setup;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.ViewModels;
using OngekiFumenEditor.Avalonia.Modules.OgkiFumenListBrowser.Models;
using OngekiFumenEditor.Avalonia.Modules.OgkiFumenListBrowser.Models.Settings;
using OngekiFumenEditor.Avalonia.Modules.OgkiFumenListBrowser.Services;
using OngekiFumenEditor.Avalonia.Parser;
using OngekiFumenEditor.Avalonia.Utils;
using OngekiFumenEditor.Avalonia.Utils.SimpleFileSystem;
using OngekiFumenEditor.Avalonia.Utils.SimpleFileSystem.Impl.AvaloniaStorageProvider;

namespace OngekiFumenEditor.Avalonia.Modules.OgkiFumenListBrowser.ViewModels;

[RegisterSingleton<IOgkiFumenListBrowser>]
public partial class OgkiFumenListBrowserViewModel : WindowViewModelBase, IOgkiFumenListBrowser, IDisposable
{
    private const int JacketDecodeWidth = 176;
    private readonly IAudioManager audioManager;
    private readonly IFumenParserManager parserManager;
    private readonly IFumenVisualEditorProvider editorProvider;
    private readonly IShell shell;
    private readonly IDialogManager dialogManager;
    private readonly IEditorRecentFilesManager recentFilesManager;
    private readonly IOgkiFumenListBrowserJacketDecoder jacketDecoder;
    private readonly SemaphoreSlim refreshGate = new(1, 1);
    private readonly ConcurrentDictionary<string, WeakReference<Bitmap>> jacketBitmapCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, Lazy<Task>> jacketLoadTasks = new(StringComparer.OrdinalIgnoreCase);
    private CancellationTokenSource? refreshCancellation;
    private ISimpleDirectory? rootDirectory;
    private bool isDisposed;
    private long refreshVersion;

    public OgkiFumenListBrowserViewModel()
        : this(
            IoC.Get<IAudioManager>(),
            IoC.Get<IFumenParserManager>(),
            IoC.Get<IFumenVisualEditorProvider>(),
            IoC.Get<IShell>(),
            IoC.Get<IDialogManager>(),
            IoC.Get<IEditorRecentFilesManager>(),
            IoC.Get<IOgkiFumenListBrowserJacketDecoder>())
    {
    }

    internal OgkiFumenListBrowserViewModel(
        IAudioManager audioManager,
        IFumenParserManager parserManager,
        IFumenVisualEditorProvider editorProvider,
        IShell shell,
        IDialogManager dialogManager,
        IEditorRecentFilesManager recentFilesManager,
        IOgkiFumenListBrowserJacketDecoder jacketDecoder)
    {
        this.audioManager = audioManager ?? throw new ArgumentNullException(nameof(audioManager));
        this.parserManager = parserManager ?? throw new ArgumentNullException(nameof(parserManager));
        this.editorProvider = editorProvider ?? throw new ArgumentNullException(nameof(editorProvider));
        this.shell = shell ?? throw new ArgumentNullException(nameof(shell));
        this.dialogManager = dialogManager ?? throw new ArgumentNullException(nameof(dialogManager));
        this.recentFilesManager = recentFilesManager ?? throw new ArgumentNullException(nameof(recentFilesManager));
        this.jacketDecoder = jacketDecoder ?? throw new ArgumentNullException(nameof(jacketDecoder));
        RootFolderDisplayName = OgkiFumenListBrowserSetting.Default.RootFolderDisplayName ?? string.Empty;
    }

    public WindowViewModelBase WindowViewModel => this;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SelectFolderCommand))]
    public partial string RootFolderDisplayName { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial string Keywords { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsBusy { get; private set; }

    [ObservableProperty]
    public partial string ErrorMessage { get; private set; } = string.Empty;

    private readonly BatchObservableCollection<OngekiFumenSet> displayFumenSets = [];

    public ObservableCollection<OngekiFumenSet> DisplayFumenSets => displayFumenSets;

    public string RootFolderPath => RootFolderDisplayName;

    public bool HasRootFolder => !string.IsNullOrWhiteSpace(RootFolderDisplayName);

    public bool HasKeywords => !string.IsNullOrWhiteSpace(Keywords);

    public bool HasResults => DisplayFumenSets.Count > 0;

    public bool ShowResults => !HasError && HasResults;

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public bool ShowErrorState => !IsBusy && HasError;

    public bool ShowNoRootState => !IsBusy && !HasError && !HasRootFolder;

    public bool ShowNoResultsState => !IsBusy && !HasError && HasRootFolder && !HasResults;

    private List<OngekiFumenSet> fumenSets = [];

    public override void OnViewAfterLoaded(Gekimini.Avalonia.Views.IView view)
    {
        base.OnViewAfterLoaded(view);
        this.view = view as global::Avalonia.Controls.Control;
        _ = RestoreSavedRootAsync();
    }

    public override void OnViewBeforeUnload(Gekimini.Avalonia.Views.IView view)
    {
        base.OnViewBeforeUnload(view);
        this.view = null;
        ReleaseSession();
    }

    [RelayCommand]
    private async Task SelectFolderAsync()
    {
        if (isDisposed)
            return;

        var topLevel = TopLevel.GetTopLevel(view);
        if (topLevel is null || !topLevel.StorageProvider.CanPickFolder)
            return;

        var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = Lang.SelectGameRootFolder,
            AllowMultiple = false
        });
        if (folders.Count == 0)
            return;

        for (var index = 1; index < folders.Count; index++)
            folders[index].Dispose();

        try
        {
            await SetRootFromStorageFolderAsync(folders[0]);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            Log.LogError("Unable to use the selected Ogki game root.", exception);
            await InvokeOnUiThreadAsync(() => ErrorMessage = Lang.OgkiBrowserScanFailed);
        }
    }

    [RelayCommand]
    private void ApplyKeywords()
    {
        var keyword = Keywords?.Trim() ?? string.Empty;
        IEnumerable<OngekiFumenSet> result = fumenSets;
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            result = fumenSets
                .Select(set => (Distance: FuzzyDistance(set, keyword), Set: set))
                .Where(x => x.Distance < 5)
                .OrderBy(x => x.Distance)
                .ThenBy(x => x.Set.MusicId)
                .Select(x => x.Set);
        }

        displayFumenSets.ReplaceAll(result);

        NotifyDisplayState();
    }

    /// <summary>
    /// Requests a jacket when its list item enters the effective viewport.
    /// </summary>
    internal void RequestJacketLoad(OngekiFumenSet set)
    {
        ArgumentNullException.ThrowIfNull(set);
        if (isDisposed || set.JacketFile is null || string.IsNullOrWhiteSpace(set.JacketLocator) || set.JacketBitmap is not null)
            return;

        var version = Volatile.Read(ref refreshVersion);
        var key = $"{version}:{set.JacketLocator}";
        var cancellationToken = refreshCancellation?.Token ?? CancellationToken.None;
        var lazyTask = jacketLoadTasks.GetOrAdd(
            key,
            _ => new Lazy<Task>(
                () => LoadJacketAsync(set, version, key, cancellationToken),
                LazyThreadSafetyMode.ExecutionAndPublication));
        _ = lazyTask.Value;
    }

    [RelayCommand]
    private Task LoadFumenAsync(OngekiFumenDiff diff) => OpenFumenAsync(diff);

    [RelayCommand]
    private Task RefreshListAsync() => RefreshAsync();

    [RelayCommand]
    private Task RetryAsync() => rootDirectory is null ? SelectFolderAsync() : RefreshAsync();

    [RelayCommand]
    private void ClearKeywords()
    {
        Keywords = string.Empty;
        ApplyKeywords();
    }

    public Task<IReadOnlyList<OngekiFumenSet>> SearchFumenSet(
        ISimpleDirectory root,
        CancellationToken cancellationToken = default) =>
        new OgkiFumenListBrowserScanner(audioManager).ScanAsync(root, cancellationToken);

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        var root = rootDirectory;
        if (root is null)
        {
            await InvokeOnUiThreadAsync(() =>
            {
                fumenSets = [];
                displayFumenSets.ReplaceAll(Array.Empty<OngekiFumenSet>());
                ErrorMessage = string.Empty;
                IsBusy = false;
                NotifyDisplayState();
            });
            return;
        }

        var version = Interlocked.Increment(ref refreshVersion);
        refreshCancellation?.Cancel();
        refreshCancellation?.Dispose();
        refreshCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        jacketLoadTasks.Clear();
        var token = refreshCancellation.Token;

        var acquiredRefreshGate = false;
        try
        {
            await refreshGate.WaitAsync(token).ConfigureAwait(false);
            acquiredRefreshGate = true;
            await InvokeOnUiThreadAsync(() =>
            {
                if (version != refreshVersion || isDisposed)
                    return;
                IsBusy = true;
                ErrorMessage = string.Empty;
            });
            var scanner = new OgkiFumenListBrowserScanner(audioManager);
            var result = await scanner.ScanAsync(root, token).ConfigureAwait(false);
            if (isDisposed || version != refreshVersion || token.IsCancellationRequested)
                return;

            var scannedSets = result.ToList();
            await InvokeOnUiThreadAsync(() =>
            {
                if (isDisposed || version != refreshVersion || token.IsCancellationRequested)
                    return;
                fumenSets = scannedSets;
                ApplyKeywords();
            });
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            Log.LogError("Ogki fumen list scan failed.", exception);
            await InvokeOnUiThreadAsync(() =>
            {
                if (isDisposed || version != refreshVersion)
                    return;
                fumenSets = [];
                displayFumenSets.ReplaceAll(Array.Empty<OngekiFumenSet>());
                ErrorMessage = Lang.OgkiBrowserScanFailed;
                NotifyDisplayState();
            });
        }
        finally
        {
            if (acquiredRefreshGate)
                refreshGate.Release();
            if (version == refreshVersion)
                await InvokeOnUiThreadAsync(() =>
                {
                    if (version == refreshVersion)
                        IsBusy = false;
                });
        }
    }

    private async Task RestoreSavedRootAsync()
    {
        var bookmark = OgkiFumenListBrowserSetting.Default.RootFolderBookmark;
        if (string.IsNullOrWhiteSpace(bookmark) || isDisposed)
            return;

        try
        {
            var storageProvider = (Application.Current as App)?.TopLevel.StorageProvider;
            if (storageProvider is null)
                return;
            var folder = await storageProvider.OpenFolderBookmarkAsync(bookmark);
            if (folder is null)
            {
                ClearSavedRoot();
                return;
            }

            await SetRootFromStorageFolderAsync(folder, saveBookmark: false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            Log.LogWarn($"The saved Ogki game-root bookmark is unavailable: {exception.Message}");
            ClearSavedRoot();
        }
    }

    private async Task SetRootFromStorageFolderAsync(IStorageFolder storageFolder, bool saveBookmark = true)
    {
        var displayName = storageFolder.Name;
        var ownedStorageFolder = storageFolder;
        storageFolder = null!;
        ISimpleDirectory? loaded = null;
        try
        {
            loaded = await AvaloniaStorageProviderFileSystemBuilder
                .LoadFromAvaloniaStorageFolder(ownedStorageFolder, CancellationToken.None)
                .ConfigureAwait(false);
            await InvokeOnUiThreadAsync(() => ReplaceRoot(loaded));
            loaded = null;

            var resolvedDisplayName = displayName ?? string.Empty;
            await InvokeOnUiThreadAsync(() => RootFolderDisplayName = resolvedDisplayName);
            string bookmark = string.Empty;
            if (saveBookmark && rootDirectory is IBookmarkableSimpleFileSystemItem bookmarkable && bookmarkable.CanBookmark)
            {
                bookmark = await bookmarkable.SaveBookmarkAsync().ConfigureAwait(false) ?? string.Empty;
            }
            if (saveBookmark)
            {
                var setting = OgkiFumenListBrowserSetting.Default;
                setting.RootFolderBookmark = bookmark;
                setting.RootFolderDisplayName = RootFolderDisplayName;
                setting.Save();
            }

            await RefreshAsync();
        }
        catch
        {
            loaded?.Dispose();
            throw;
        }
    }

    private void ReplaceRoot(ISimpleDirectory next)
    {
        Interlocked.Increment(ref refreshVersion);
        refreshCancellation?.Cancel();
        jacketLoadTasks.Clear();
        foreach (var set in fumenSets)
            set.JacketBitmap = null;
        fumenSets = [];
        displayFumenSets.ReplaceAll(Array.Empty<OngekiFumenSet>());
        ErrorMessage = string.Empty;
        IsBusy = false;
        NotifyDisplayState();
        ClearJacketCache();
        var previous = rootDirectory;
        rootDirectory = next;
        previous?.Dispose();
    }

    private void ClearSavedRoot()
    {
        RootFolderDisplayName = string.Empty;
        ErrorMessage = string.Empty;
        var setting = OgkiFumenListBrowserSetting.Default;
        setting.RootFolderBookmark = string.Empty;
        setting.RootFolderDisplayName = string.Empty;
        setting.Save();
    }

    partial void OnRootFolderDisplayNameChanged(string value) => NotifyDisplayState();

    partial void OnKeywordsChanged(string value) => OnPropertyChanged(nameof(HasKeywords));

    partial void OnIsBusyChanged(bool value) => NotifyDisplayState();

    partial void OnErrorMessageChanged(string value) => NotifyDisplayState();

    private void NotifyDisplayState()
    {
        OnPropertyChanged(nameof(HasRootFolder));
        OnPropertyChanged(nameof(HasResults));
        OnPropertyChanged(nameof(ShowResults));
        OnPropertyChanged(nameof(HasError));
        OnPropertyChanged(nameof(ShowErrorState));
        OnPropertyChanged(nameof(ShowNoRootState));
        OnPropertyChanged(nameof(ShowNoResultsState));
    }

    private static async Task InvokeOnUiThreadAsync(Action action)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            action();
            return;
        }

        await Dispatcher.UIThread.InvokeAsync(action);
    }

    private async Task OpenFumenAsync(OngekiFumenDiff diff)
    {
        if (diff?.FumenFile is null || diff.RefSet.AudioFile is null || isDisposed)
            return;

        IsBusy = true;
        EditorContext? context = null;
        Gekimini.Avalonia.Framework.IDocumentViewModel? editor = null;
        byte[]? recentData = null;
        var ownershipTransferred = false;
        try
        {
            var deserializer = parserManager.GetDeserializer(diff.FumenFile.FileName);
            if (deserializer is null)
                return;

            await using var fumenStream = await diff.FumenFile.OpenReadAsync();
            var fumen = await deserializer.DeserializeAsync(fumenStream);

            context = new EditorContext
            {
                Fumen = fumen,
                FileAccessContext = await CreateEditorFileAccessContextAsync(diff)
            };
            recentData = await TryCreateRecentDataAsync(context.FileAccessContext).ConfigureAwait(false);
            editor = editorProvider.Create();
            if (!await editorProvider.TryOpen(editor, context))
            {
                await dialogManager.ShowMessageDialog(
                    Lang.CantOpenByAudioFileNotFound.Format(diff.RefSet.Title),
                    DialogMessageType.Error);
                return;
            }
            context = null;
            if (editor is FumenVisualEditorViewModel editorViewModel)
                editorViewModel.DisplayName = $"[FastOpen] {diff.RefSet.Title}";
            await shell.OpenDocumentAsync(editor);
            ownershipTransferred = true;
            TryPostRecentRecord(diff, editor, recentData);
        }
        catch (Exception exception)
        {
            Log.LogError("Failed to open fumen from the list browser.", exception);
            await dialogManager.ShowMessageDialog(
                $"{Lang.CantLoadFumen}{exception.Message}", DialogMessageType.Error);
        }
        finally
        {
            if (!ownershipTransferred)
            {
                context?.Dispose();
                if (editor is IDisposable disposable)
                    disposable.Dispose();
            }
            await InvokeOnUiThreadAsync(() => IsBusy = false);
        }
    }

    private async Task<EditorFileAccessContext> CreateEditorFileAccessContextAsync(OngekiFumenDiff diff)
    {
        var fumenFile = diff.FumenFile;
        var audioFile = diff.RefSet.AudioFile!;
        var awbFile = diff.RefSet.AudioAwbFile;
        var root = rootDirectory;
        if (root is not null && root is IBookmarkableSimpleFileSystemItem bookmarkable && bookmarkable.CanBookmark)
        {
            try
            {
                var contextRoot = await CloneRootFromBookmarkAsync(bookmarkable).ConfigureAwait(false);
                var clonedFumen = OgkiFumenListBrowserPath.ResolveFile(contextRoot, diff.FumenLocator);
                var clonedAudio = OgkiFumenListBrowserPath.ResolveFile(contextRoot, diff.RefSet.AudioLocator);
                var clonedAwb = diff.RefSet.AudioAwbLocator is { } awbLocator
                    ? OgkiFumenListBrowserPath.ResolveFile(contextRoot, awbLocator)
                    : null;
                if (clonedFumen is null || clonedAudio is null ||
                    (diff.RefSet.AudioAwbLocator is not null && clonedAwb is null))
                {
                    contextRoot.Dispose();
                }
                else
                {
                    return new EditorFileAccessContext
                    {
                        ProjectDirectory = contextRoot,
                        FumenFile = clonedFumen,
                        AudioFile = clonedAudio,
                        AudioAwbFile = clonedAwb
                    };
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException)
            {
                Log.LogWarn($"The browser root could not be reopened from its bookmark: {exception.Message}");
            }
        }

        var context = EditorFileAccessContext.Create(
            fumenFile: fumenFile,
            audioFile: audioFile,
            audioAwbFile: awbFile);
        return context;
    }

    private async Task<ISimpleDirectory> CloneRootFromBookmarkAsync(IBookmarkableSimpleFileSystemItem bookmarkable)
    {
        var storageProvider = (Application.Current as App)?.TopLevel.StorageProvider
            ?? throw new InvalidOperationException("No active storage provider is available.");
        var bookmark = await bookmarkable.SaveBookmarkAsync().ConfigureAwait(false)
            ?? throw new IOException("The selected directory could not be bookmarked.");
        var folder = await storageProvider.OpenFolderBookmarkAsync(bookmark).ConfigureAwait(false)
            ?? throw new IOException("The selected directory bookmark is no longer available.");
        return await AvaloniaStorageProviderFileSystemBuilder.LoadFromAvaloniaStorageFolder(folder)
            .ConfigureAwait(false);
    }

    private static async Task<byte[]?> TryCreateRecentDataAsync(EditorFileAccessContext? context)
    {
        if (context is null)
            return null;
        try
        {
            return (await context.ToSnapshotAsync().ConfigureAwait(false)).Serialize();
        }
        catch (Exception exception)
        {
            Log.LogWarn($"The fumen opened by the list browser cannot be bookmarked: {exception.Message}");
            return null;
        }
    }

    private void TryPostRecentRecord(
        OngekiFumenDiff diff,
        Gekimini.Avalonia.Framework.IDocumentViewModel editor,
        byte[]? recentData)
    {
        if (recentData is null)
            return;
        try
        {
            var name = editor is FumenVisualEditorViewModel viewModel
                ? viewModel.DisplayName
                : diff.RefSet.Title;
            recentFilesManager.PostRecent(
                FumenVisualEditorProviderBase.FileType,
                name,
                diff.FumenLocator,
                recentData);
        }
        catch (Exception exception)
        {
            Log.LogWarn($"Unable to store a recent fumen record: {exception.Message}");
        }
    }

    private async Task LoadJacketAsync(
        OngekiFumenSet set,
        long version,
        string requestKey,
        CancellationToken cancellationToken)
    {
        try
        {
            if (set.JacketFile is null || string.IsNullOrWhiteSpace(set.JacketLocator))
                return;

            if (jacketBitmapCache.TryGetValue(set.JacketLocator, out var weak) && weak.TryGetTarget(out var cached))
            {
                await PublishJacketBitmapAsync(set, cached, version, disposeWhenStale: false).ConfigureAwait(false);
                return;
            }

            var bytes = await jacketDecoder.LoadPngBytesAsync(set.JacketFile, cancellationToken).ConfigureAwait(false);
            if (bytes is null or { Length: 0 })
                return;

            await using var stream = new MemoryStream(bytes, writable: false);
            var bitmap = Bitmap.DecodeToWidth(stream, JacketDecodeWidth, BitmapInterpolationMode.MediumQuality);
            if (!IsJacketRequestCurrent(set, version))
            {
                bitmap.Dispose();
                return;
            }

            if (await PublishJacketBitmapAsync(set, bitmap, version, disposeWhenStale: true).ConfigureAwait(false) &&
                IsJacketRequestCurrent(set, version))
            {
                jacketBitmapCache[set.JacketLocator] = new WeakReference<Bitmap>(bitmap);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            Log.LogDebug($"Unable to load Ogki jacket for '{set.JacketLocator}': {exception.Message}");
        }
        finally
        {
            jacketLoadTasks.TryRemove(requestKey, out _);
        }
    }

    private async Task<bool> PublishJacketBitmapAsync(
        OngekiFumenSet set,
        Bitmap bitmap,
        long version,
        bool disposeWhenStale)
    {
        var published = false;
        await global::Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (!IsJacketRequestCurrent(set, version))
            {
                if (disposeWhenStale)
                    bitmap.Dispose();
                return;
            }

            set.JacketBitmap = bitmap;
            published = true;
        }).GetTask().ConfigureAwait(false);
        return published;
    }

    private bool IsJacketRequestCurrent(OngekiFumenSet set, long version) =>
        !isDisposed &&
        version == Volatile.Read(ref refreshVersion) &&
        set.JacketFile is not null &&
        !string.IsNullOrWhiteSpace(set.JacketLocator);

    private static int FuzzyDistance(OngekiFumenSet set, string keyword)
    {
        var best = int.MaxValue;
        if (!string.IsNullOrWhiteSpace(set.Artist))
        {
            best = Math.Min(best, LevenshteinDistance(set.Artist, keyword));
            if (best == 0)
                return best;
        }

        if (!string.IsNullOrWhiteSpace(set.Title))
        {
            best = Math.Min(best, LevenshteinDistance(set.Title, keyword));
            if (best == 0)
                return best;
        }

        foreach (var diff in set.Difficults)
        {
            if (string.IsNullOrWhiteSpace(diff.Creator))
                continue;

            best = Math.Min(best, LevenshteinDistance(diff.Creator, keyword));
            if (best == 0)
                return best;
        }

        return best;
    }

    private static int LevenshteinDistance(string left, string right)
    {
        if (left.Length == 0)
            return right.Length;
        if (right.Length == 0)
            return left.Length;

        if (left.Contains(right, StringComparison.InvariantCultureIgnoreCase) ||
            right.Contains(left, StringComparison.InvariantCultureIgnoreCase))
            return 0;
        left = left.ToLowerInvariant();
        right = right.ToLowerInvariant();
        if (right.Length > left.Length)
            (left, right) = (right, left);

        var previous = new int[right.Length + 1];
        var current = new int[right.Length + 1];
        for (var index = 0; index < previous.Length; index++)
            previous[index] = index;

        for (var i = 1; i <= left.Length; i++)
        {
            current[0] = i;
            for (var j = 1; j <= right.Length; j++)
                current[j] = Math.Min(Math.Min(current[j - 1] + 1, previous[j] + 1), previous[j - 1] + (left[i - 1] == right[j - 1] ? 0 : 1));
            (previous, current) = (current, previous);
        }
        return previous[^1];
    }

    private void ClearJacketCache()
    {
        foreach (var weak in jacketBitmapCache.Values)
            if (weak.TryGetTarget(out var bitmap))
                bitmap.Dispose();
        jacketBitmapCache.Clear();
    }

    public void Dispose()
    {
        if (isDisposed)
            return;
        isDisposed = true;
        ReleaseSession();
        GC.SuppressFinalize(this);
    }

    private void ReleaseSession()
    {
        Interlocked.Increment(ref refreshVersion);
        refreshCancellation?.Cancel();
        refreshCancellation?.Dispose();
        refreshCancellation = null;
        jacketLoadTasks.Clear();
        rootDirectory?.Dispose();
        rootDirectory = null;
        fumenSets = [];
        displayFumenSets.ReplaceAll(Array.Empty<OngekiFumenSet>());
        ErrorMessage = string.Empty;
        IsBusy = false;
        NotifyDisplayState();
        ClearJacketCache();
    }

    private sealed class BatchObservableCollection<T> : ObservableCollection<T>
    {
        public void ReplaceAll(IEnumerable<T> items)
        {
            ArgumentNullException.ThrowIfNull(items);
            var next = items as IReadOnlyList<T> ?? items.ToArray();
            if (Count == next.Count && this.SequenceEqual(next))
                return;

            CheckReentrancy();
            Items.Clear();
            foreach (var item in next)
                Items.Add(item);

            OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));
            OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }
    }

    // The picker is invoked from the view command; keeping its anchor as a short-lived field
    // avoids caching a TopLevel or storage provider across windows.
    private global::Avalonia.Controls.Control? view;
}
