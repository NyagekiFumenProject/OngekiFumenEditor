using CommunityToolkit.Mvvm.ComponentModel;
using Gekimini.Avalonia.Framework.Documents;
using Gekimini.Avalonia.Framework.Languages;
using OngekiFumenEditor.Avalonia.Kernel.Audio;
using OngekiFumenEditor.Avalonia.Base;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Base;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Models;
using OngekiFumenEditor.Avalonia.Utils;

namespace OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.ViewModels;

public partial class FumenVisualEditorViewModel : DocumentViewModelBase
{
    public delegate void LoadingFinishedEventHandler(object sender, EditorProjectDataModel args);
    public event LoadingFinishedEventHandler LoadingFinished;
    public EditorSetting Setting { get; } = new();

    [ObservableProperty]
    public partial OngekiFumen Fumen { get; set; }

    [ObservableProperty]
    public partial EditorProjectDataModel EditorProjectData { get; set; }

    [ObservableProperty]
    public partial string FilePath { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string FileName { get; set; } = "Untitled";

    [ObservableProperty]
    public partial string DisplayName { get; set; } = "Untitled";

    [ObservableProperty]
    public partial bool IsBatchMode { get; set; }

    public IAudioPlayer AudioPlayer { get; set; }

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

    public Task<bool> New()
    {
        EditorProjectData = new EditorProjectDataModel
        {
            Fumen = new OngekiFumen()
        };
        Fumen = EditorProjectData.Fumen;
        FilePath = string.Empty;
        FileName = "Untitled";
        DisplayName = "[New] Untitled";
        UpdateTitle();
        LoadingFinished?.Invoke(this, EditorProjectData);
        return Task.FromResult(true);
    }

    public Task<bool> Load()
    {
        Log.LogWarn("FumenVisualEditorViewModel.Load() without file path is not supported yet.");
        return Task.FromResult(false);
    }

    public async Task<bool> Load(string projectFilePath)
    {
        if (string.IsNullOrWhiteSpace(projectFilePath) || !File.Exists(projectFilePath))
            return false;

        var data = await EditorProjectDataUtils.TryLoadFromFileAsync(projectFilePath);
        return await Load(data, projectFilePath);
    }

    public Task<bool> Load(EditorProjectDataModel project)
    {
        var sourcePath = project?.FumenFile is null
            ? project?.FumenFilePath
            : null;
        return Load(project, sourcePath);
    }

    private Task<bool> Load(EditorProjectDataModel project, string sourcePath)
    {
        if (project is null)
            return Task.FromResult(false);

        EditorProjectData = project;
        Fumen = project.Fumen ?? new OngekiFumen();
        FilePath = sourcePath ?? string.Empty;
        FileName = project.FumenFile?.FileName ??
            (string.IsNullOrWhiteSpace(FilePath) ? "Untitled" : Path.GetFileName(FilePath));
        DisplayName = FileName;
        UpdateTitle();
        LoadingFinished?.Invoke(this, EditorProjectData);
        return Task.FromResult(true);
    }

    public async Task<bool> Save(string projectFilePath)
    {
        if (string.IsNullOrWhiteSpace(projectFilePath) || EditorProjectData is null)
            return false;

        EditorProjectData.Fumen = Fumen;
        var saveResult = await EditorProjectDataUtils.TrySaveEditorAsync(projectFilePath, EditorProjectData);
        if (!saveResult.IsSuccess)
        {
            Log.LogError(saveResult.ErrorMessage);
            return false;
        }

        FilePath = projectFilePath;
        FileName = Path.GetFileName(projectFilePath);
        DisplayName = FileName;
        UpdateTitle();
        return true;
    }

    public async Task<bool> SaveSelectedFumenFile()
    {
        if (EditorProjectData?.FumenFile is not { } fumenFile)
            return false;

        EditorProjectData.Fumen = Fumen;
        var saveResult = await EditorProjectDataUtils.TrySaveFumenFileAsync(fumenFile, EditorProjectData);
        if (!saveResult.IsSuccess)
        {
            Log.LogError(saveResult.ErrorMessage);
            return false;
        }

        return true;
    }

    public void ScrollTo(OngekiTimelineObjectBase ongekiObject)
    {
        ScrollTo((ITimelineObject)ongekiObject);
    }

    public void NotifyObjectClicked(OngekiTimelineObjectBase ongekiObject)
    {
    }

    public void KeyboardAction_HideOrShow(object _)
    {
        IsUserRequestHideEditorObject = !IsPreviewMode;
    }

    public void KeyboardAction_PlayOrPause(object _)
    {
        if (AudioPlayer is null)
            return;
        if (AudioPlayer.IsPlaying)
            AudioPlayer.Pause();
        else
            AudioPlayer.Play();
    }

    public void RecalculateTotalDurationHeight()
    {
    }

    private void UpdateTitle()
    {
        Title = LocalizedString.CreateFromRawText(DisplayName);
    }
}

