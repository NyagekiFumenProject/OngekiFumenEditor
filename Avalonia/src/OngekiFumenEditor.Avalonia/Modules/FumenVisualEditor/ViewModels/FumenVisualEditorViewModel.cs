using CommunityToolkit.Mvvm.ComponentModel;
using Gekimini.Avalonia.Framework.Documents;
using Gekimini.Avalonia.Framework.Languages;
using Gekimini.Avalonia.Platforms.Services.Window;
using OngekiFumenEditor.Avalonia.Kernel.Audio;
using OngekiFumenEditor.Avalonia.Base;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Base;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Models;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.ViewModels.Dialogs;
using OngekiFumenEditor.Avalonia.Utils;
using OngekiFumenEditor.Avalonia.Utils.SimpleFileSystem;
using OngekiFumenEditor.Avalonia.Utils.SimpleFileSystem.Impl.LocalFileSystem;

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

    public async Task<bool> New()
    {
        var dialogViewModel = new EditorProjectSetupDialogViewModel();
        var result = await IoC.Get<IWindowManager>().ShowDialogAsync(dialogViewModel);
        if (result != true)
        {
            Log.LogInfo(Assets.Languages.Lang.CloseEditorByProjectSetupFail);
            return false;
        }

        var project = dialogViewModel.EditorProjectData;
        if (!await Load(project))
        {
            project.DisposeRuntimeFiles();
            return false;
        }

        FilePath = string.Empty;
        FileName = "Untitled";
        DisplayName = "[New] Untitled";
        UpdateTitle();
        return true;
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

    private async Task<bool> Load(EditorProjectDataModel project, string sourcePath)
    {
        if (project is null)
            return false;

        var audioFile = GetAudioFile(project);
        if (audioFile is null)
            return false;

        var audioPlayer = await IoC.Get<IAudioManager>().LoadAudioAsync(audioFile);
        AudioPlayer?.Dispose();
        AudioPlayer = audioPlayer;
        EditorProjectData = project;
        Fumen = project.Fumen ?? new OngekiFumen();
        FilePath = sourcePath ?? string.Empty;
        FileName = project.FumenFile?.FileName ??
            (string.IsNullOrWhiteSpace(FilePath) ? "Untitled" : Path.GetFileName(FilePath));
        DisplayName = FileName;
        UpdateTitle();
        LoadingFinished?.Invoke(this, EditorProjectData);
        return true;
    }

    private static ISimpleFile GetAudioFile(EditorProjectDataModel project)
    {
        if (project.AudioFile is not null)
            return project.AudioFile;

        if (string.IsNullOrWhiteSpace(project.AudioFilePath) || !File.Exists(project.AudioFilePath))
            return null;

        project.AudioFile = new LocalSimpleFile(project.AudioFilePath);
        return project.AudioFile;
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

