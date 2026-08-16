using CommunityToolkit.Mvvm.ComponentModel;
using Gekimini.Avalonia.Framework;
using Gekimini.Avalonia.Framework.Documents;
using Gekimini.Avalonia.Framework.Languages;
using Gekimini.Avalonia.Framework.RecentFiles;
using Gekimini.Avalonia.Framework.UndoRedo;
using Gekimini.Avalonia.Platforms.Services.MainWindow;
using Gekimini.Avalonia.Modules.Shell.Commands;
using OngekiFumenEditor.Avalonia.Kernel.Audio;
using OngekiFumenEditor.Avalonia.Modules.AudioPlayerToolViewer;
using OngekiFumenEditor.Avalonia.Base;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Base;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Kernel;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Models;
using OngekiFumenEditor.Avalonia.Utils;
using OngekiFumenEditor.Avalonia.Utils.SimpleFileSystem;
using OngekiFumenEditor.Avalonia.Utils.DeadHandler;
using System.ComponentModel;

namespace OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.ViewModels;

public partial class FumenVisualEditorViewModel : DocumentViewModelBase, IPersistedDocumentViewModel, IDisposable
{
    public delegate void LoadingFinishedEventHandler(object sender, EditorProjectDataModel args);
    public event LoadingFinishedEventHandler LoadingFinished;
    public EditorSetting Setting { get; } = new();

    [ObservableProperty]
    public partial OngekiFumen Fumen { get; set; }

    [ObservableProperty]
    public partial EditorProjectDataModel EditorProjectData { get; set; }

    // 运行时上下文：持有谱面对象、文件能力和会话期状态，与纯数据模型 EditorProjectData 配对使用。
    [ObservableProperty]
    public partial EditorContext EditorContext { get; set; }

    [ObservableProperty]
    public partial string FilePath { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string FileName { get; set; } = "Untitled";

    private string explicitDisplayName;

    // 对齐 WPF PersistedDocument.DisplayName 的计算语义：
    // 显式名（如 "[快速打开] xxx"）为空时回退到 FileName/FilePath，脏状态时前缀 "* "。
    public string DisplayName
    {
        get
        {
            var name = explicitDisplayName;
            if (string.IsNullOrWhiteSpace(name))
                name = string.IsNullOrWhiteSpace(FileName) ? FilePath : FileName;
            if (IsDirty)
                name = "* " + name;
            return name;
        }
        set
        {
            if (SetProperty(ref explicitDisplayName, value))
                UpdateTitle();
        }
    }

    [ObservableProperty]
    public partial bool IsBatchMode { get; set; }

    [ObservableProperty]
    public partial bool IsDirty { get; set; }

    public bool IsNew => EditorContext?.ProjectFile is null;

    public override IEnumerable<Type> SupportCommandDefinitionTypes =>
        base.SupportCommandDefinitionTypes.Where(type => type != typeof(SaveFileAsCommandDefinition));

    public IAudioPlayer AudioPlayer { get; set; }

    internal Guid RecoverySnapshotId { get; } = Guid.NewGuid();

    private bool areRuntimeSubscriptionsAttached;
    private bool isDisposed;

    internal bool IsDisposed => isDisposed;

    private bool isShowCurveControlAlways = false;
    public bool IsShowCurveControlAlways
    {
        get => isShowCurveControlAlways;
        set
        {
            SetProperty(ref isShowCurveControlAlways, value);
            ToastNotify($"ShowCurveControlAlways: {(IsShowCurveControlAlways ? "Enable" : "Disable")}");
        }
    }

    private bool hideWallLaneWhenEnablePlayField;
    public bool HideWallLaneWhenEnablePlayField => hideWallLaneWhenEnablePlayField;

    public FumenVisualEditorViewModel()
    {
        SelectionArea = new(this);
    }

    partial void OnFumenChanged(OngekiFumen oldValue, OngekiFumen newValue)
    {
        DetachFumenSubscriptions(oldValue);
        AttachFumenSubscriptions(newValue);

        if (EditorContext is not null && !ReferenceEquals(EditorContext.Fumen, newValue))
            EditorContext.Fumen = newValue;

        RecalculateTotalDurationHeight();
    }

    partial void OnEditorContextChanged(EditorContext oldValue, EditorContext newValue)
    {
        OnPropertyChanged(nameof(IsNew));
    }

    partial void OnEditorProjectDataChanged(EditorProjectDataModel oldValue, EditorProjectDataModel newValue)
    {
        if (oldValue is not null)
            oldValue.PropertyChanged -= OnEditorProjectDataPropertyChanged;
        if (newValue is not null)
            newValue.PropertyChanged += OnEditorProjectDataPropertyChanged;

        RecalculateTotalDurationHeight();

        // 对齐 WPF：项目数据变化时，若自身是活动编辑器则刷新主窗口标题。
        try
        {
            if (IoC.Get<IEditorDocumentManager>().CurrentActivatedEditor == this)
                IoC.Get<IPlatformMainWindow>().Title = $"Ongeki Fumen Editor - {DisplayName} ";
        }
        catch (InvalidOperationException)
        {
            // 无 GUI 外壳的环境（如单元测试）下忽略窗口标题刷新。
        }
    }

    partial void OnFileNameChanged(string value) => RefreshDisplayName();

    partial void OnFilePathChanged(string value) => RefreshDisplayName();

    partial void OnIsDirtyChanged(bool value) => RefreshDisplayName();

    private void RefreshDisplayName()
    {
        OnPropertyChanged(nameof(DisplayName));
        UpdateTitle();
    }

    public virtual Task<bool> New()
    {
        Log.LogWarn("FumenVisualEditor does not currently support creating a project without an existing project folder.");
        return Task.FromResult(false);
    }

    Task<bool> IPersistedDocumentViewModel.Load() =>
        IoC.Get<IFumenVisualEditorProvider>().TryOpen(this);

    Task<bool> IPersistedDocumentViewModel.Load(RecentRecordInfo recordInfo) =>
        IoC.Get<IFumenVisualEditorProvider>().TryOpen(this, recordInfo);

    internal async Task<bool> LoadProjectAsync(EditorContext context, string sourcePath)
    {
        if (context?.ProjectData is null)
            return false;

        var audioFile = context.AudioFile;
        if (audioFile is null)
            return false;

        var audioPlayer = await IoC.Get<IAudioManager>()
            .LoadProjectAudioAsync(audioFile, context.AudioAwbFile);
        AudioPlayer?.Dispose();
        AudioPlayer = audioPlayer;
        EditorContext = context;
        EditorProjectData = context.ProjectData;
        Fumen = context.Fumen ?? new OngekiFumen();
        FilePath = sourcePath ?? string.Empty;
        FileName = context.FumenFile?.FileName ??
            (string.IsNullOrWhiteSpace(FilePath) ? "Untitled" : Path.GetFileName(FilePath));
        DisplayName = default;
        RecalculateTotalDurationHeight();
        ScrollTo(context.ProjectData.RememberLastDisplayTime);
        UndoRedoManager.Clear();
        IsDirty = false;
        LoadingFinished?.Invoke(this, EditorProjectData);
        return true;
    }

    public async Task<bool> Save()
    {
        if (EditorContext?.ProjectFile is not { } projectFile)
            return false;

        EditorProjectData.RememberLastDisplayTime = CurrentPlayTime;
        EditorContext.Fumen = Fumen;
        using var ioLease = await EditorProjectIoGate.EnterAsync();
        var saveResult = await EditorProjectDataUtils.TrySaveEditorAsync(projectFile, EditorContext);
        if (!saveResult.IsSuccess)
        {
            Log.LogError(saveResult.ErrorMessage);
            return false;
        }

        try
        {
            await FumenRescue.DeleteRecoverySnapshotAsync(this);
        }
        catch (Exception exception)
        {
            Log.LogWarn($"Unable to delete recovery snapshot after saving '{FileName}': {exception.Message}");
        }

        IsDirty = false;
        // 对齐 WPF：保存成功后重置显式名（去掉 "[快速打开]" 前缀），回退到文件名。
        DisplayName = default;
        return true;
    }

    public Task<bool> SaveAs()
    {
        Log.LogWarn("Save As is unavailable until a project-folder-scoped destination flow is implemented.");
        return Task.FromResult(false);
    }

    public void ScrollTo(OngekiTimelineObjectBase ongekiObject)
    {
        ScrollTo((ITimelineObject)ongekiObject);
    }

    public void NotifyObjectClicked(OngekiTimelineObjectBase ongekiObject)
    {
        NotifyObjectClicked((OngekiObjectBase)ongekiObject);
    }

    public void KeyboardAction_HideOrShow(object _)
    {
        IsUserRequestHideEditorObject = !IsPreviewMode;
    }

    public void KeyboardAction_PlayOrPause(object _)
    {
        // 对齐 WPF：转发给音频工具面板统一处理（含音效 Prepare/Seek、回滚起始时间等）。
        IoC.Get<IAudioPlayerToolViewer>().RequestPlayOrPause();
    }

    public void RecalculateTotalDurationHeight()
    {
        if (Fumen is null)
        {
            TotalDurationHeight = ViewHeight;
            return;
        }

        var duration = EditorProjectData?.AudioDuration is { } projectDuration && projectDuration > TimeSpan.Zero
            ? projectDuration
            : AudioPlayer?.Duration ?? TimeSpan.Zero;
        var endTGrid = TGridCalculator.ConvertAudioTimeToTGrid(duration, this);
        TotalDurationHeight = ConvertToY(endTGrid.TotalUnit, Fumen.SoflansMap.DefaultSoflanList);
    }

    private void AttachFumenSubscriptions(OngekiFumen fumen)
    {
        if (fumen is null)
            return;

        fumen.BpmList.OnChangedEvent += OnTimeSignatureListChanged;
        fumen.MeterChanges.OnChangedEvent += OnTimeSignatureListChanged;
        fumen.ObjectModifiedChanged += OnFumenObjectModifiedChanged;
    }

    private void DetachFumenSubscriptions(OngekiFumen fumen)
    {
        if (fumen is null)
            return;

        fumen.BpmList.OnChangedEvent -= OnTimeSignatureListChanged;
        fumen.MeterChanges.OnChangedEvent -= OnTimeSignatureListChanged;
        fumen.ObjectModifiedChanged -= OnFumenObjectModifiedChanged;
    }

    private void AttachRuntimeSubscriptions()
    {
        if (areRuntimeSubscriptionsAttached)
            return;

        Setting.PropertyChanged += OnSettingPropertyChanged;
        ApplyUndoHistoryLimit();
        UndoRedoManager.PropertyChanged += OnUndoRedoManagerPropertyChanged;
        areRuntimeSubscriptionsAttached = true;
    }

    private void DetachRuntimeSubscriptions()
    {
        if (!areRuntimeSubscriptionsAttached)
            return;

        Setting.PropertyChanged -= OnSettingPropertyChanged;
        UndoRedoManager.PropertyChanged -= OnUndoRedoManagerPropertyChanged;
        areRuntimeSubscriptionsAttached = false;
    }

    private void OnEditorProjectDataPropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(EditorProjectDataModel.AudioDuration):
                RecalculateTotalDurationHeight();
                IsDirty = true;
                break;
            case nameof(EditorProjectDataModel.RememberLastDisplayTime):
                break;
            default:
                IsDirty = true;
                break;
        }
    }

    private void OnFumenObjectModifiedChanged(OngekiObjectBase sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(ISelectableObject.IsSelected):
            case nameof(ConnectableChildObjectBase.IsAnyControlSelecting):
                break;
            default:
                IsDirty = true;
                break;
        }
    }

    private void OnUndoRedoManagerPropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(IUndoRedoManager.UndoActionCount))
            IsDirty = true;

        if (e.PropertyName is nameof(IUndoRedoManager.UndoActionCount) or nameof(IUndoRedoManager.RedoActionCount))
            ApplyUndoHistoryLimit();
    }

    private void OnSettingPropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(EditorGlobalSetting.VerticalDisplayScale):
                var currentTGrid = Fumen is null ? null : GetCurrentTGrid();
                RecalculateTotalDurationHeight();
                if (currentTGrid is not null)
                    ScrollTo(currentTGrid);
                break;
            case nameof(EditorGlobalSetting.PlayFieldBackgroundColor):
                playFieldBackgroundColor = System.Drawing.Color
                    .FromArgb(EditorGlobalSetting.Default.PlayFieldBackgroundColor)
                    .ToVector4();
                break;
            case nameof(EditorGlobalSetting.EnablePlayFieldDrawing):
                enablePlayFieldDrawing = EditorGlobalSetting.Default.EnablePlayFieldDrawing;
                break;
            case nameof(EditorGlobalSetting.HideWallLaneWhenEnablePlayField):
                hideWallLaneWhenEnablePlayField = EditorGlobalSetting.Default.HideWallLaneWhenEnablePlayField;
                break;
            case nameof(EditorGlobalSetting.EnableShowPlayerLocation):
                PlayerLocationRecorder.Clear();
                break;
            case nameof(EditorGlobalSetting.LimitFPS):
                UpdateActualRenderInterval();
                break;
            case nameof(EditorGlobalSetting.IsEnableUndoActionSavingLimit):
            case nameof(EditorGlobalSetting.UndoActionSavingLimit):
                ApplyUndoHistoryLimit();
                break;
        }
    }

    private void ApplyUndoHistoryLimit()
    {
        var setting = EditorGlobalSetting.Default;
        if (!setting.IsEnableUndoActionSavingLimit)
        {
            UndoRedoManager.UndoCountLimit = null;
            return;
        }

        var configuredLimit = Math.Max(0, setting.UndoActionSavingLimit);
        // A pending redo chain must stay contiguous; trim executed history first, then converge to the configured limit.
        UndoRedoManager.UndoCountLimit = Math.Max(configuredLimit, UndoRedoManager.RedoActionCount);
    }

    private void UpdateTitle()
    {
        Title = LocalizedString.CreateFromRawText(DisplayName);
    }

    public void Dispose()
    {
        if (isDisposed)
            return;

        isDisposed = true;
        DetachBatchModeBehavior();
        DetachRuntimeSubscriptions();
        DisposeRenderResources();
        DetachFumenSubscriptions(Fumen);
        if (EditorProjectData is not null)
        {
            EditorProjectData.PropertyChanged -= OnEditorProjectDataPropertyChanged;
        }

        EditorContext?.Dispose();

        AudioPlayer?.Dispose();
        AudioPlayer = null;
        Setting.Dispose();
        UndoRedoManager.Clear();
        PlayerLocationRecorder.Clear();
        hits.Clear();
        cacheObjectAudioTime.Clear();
        InteractiveManager = null;
        View = null;
        LoadingFinished = null;
        Fumen = null;
        EditorProjectData = null;
        EditorContext = null;
    }
}
