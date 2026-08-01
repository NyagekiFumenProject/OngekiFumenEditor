using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using Gekimini.Avalonia.Framework.Dialogs;
using Gekimini.Avalonia.Framework.Languages;
using Gekimini.Avalonia.Modules.Window.ViewModels;
using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Assets.Languages;
using OngekiFumenEditor.Avalonia.Base;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Kernel;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.ViewModels;
using OngekiFumenEditor.Avalonia.Utils;

namespace OngekiFumenEditor.Avalonia.Modules.AudioAdjustWindow.ViewModels;

[RegisterSingleton<IAudioAdjustWindow>]
public partial class AudioAdjustWindowViewModel : WindowViewModelBase, IAudioAdjustWindow
{
    private static readonly (string ext, string desc)[] WavFileFilter = [(".wav", ".wav Audio File")];
    private readonly IEditorDocumentManager editorDocumentManager;

    private string inputFumenFilePath = string.Empty;
    public string InputFumenFilePath
    {
        get => inputFumenFilePath;
        set => SetProperty(ref inputFumenFilePath, value);
    }

    private string outputFumenFilePath = string.Empty;
    public string OutputFumenFilePath
    {
        get => outputFumenFilePath;
        set => SetProperty(ref outputFumenFilePath, value);
    }

    private bool isUseInputFile = true;
    public bool IsUseInputFile
    {
        get => isUseInputFile;
        set
        {
            var nextValue = CurrentEditorName is null || value;
            if (!SetProperty(ref isUseInputFile, nextValue))
                return;

            OnPropertyChanged(nameof(IsCurrentEditorAsInputFumen));
            OnPropertyChanged(nameof(Bpm));
        }
    }

    public bool IsCurrentEditorAsInputFumen
    {
        get => !IsUseInputFile;
        set => IsUseInputFile = !value;
    }

    public string CurrentEditorName => editorDocumentManager.CurrentActivatedEditor?.DisplayName;

    private float unit;
    public float Unit
    {
        get => unit;
        set => SetProperty(ref unit, value);
    }

    private int grid;
    public int Grid
    {
        get => grid;
        set => SetProperty(ref grid, value);
    }

    private float seconds;
    public float Seconds
    {
        get => seconds;
        set => SetProperty(ref seconds, value);
    }

    private double? bpm;
    public double Bpm
    {
        get => IsCurrentEditorAsInputFumen
            ? editorDocumentManager.CurrentActivatedEditor?.Fumen?.BpmList?.FirstBpm ?? 0
            : bpm ?? 0;
        set => SetProperty(ref bpm, value);
    }

    private bool isUseGridOffset;
    public bool IsUseGridOffset
    {
        get => isUseGridOffset;
        set => SetProperty(ref isUseGridOffset, value);
    }

    private bool isRecalculateObjects;
    public bool IsRecalculateObjects
    {
        get => isRecalculateObjects;
        set => SetProperty(ref isRecalculateObjects, value);
    }

    public AudioAdjustWindowViewModel(IEditorDocumentManager editorDocumentManager)
    {
        this.editorDocumentManager = editorDocumentManager;
        editorDocumentManager.OnActivateEditorChanged += OnActivateEditorChanged;
    }

    private void OnActivateEditorChanged(FumenVisualEditorViewModel @new, FumenVisualEditorViewModel old)
    {
        OnPropertyChanged(nameof(CurrentEditorName));
        OnPropertyChanged(nameof(Bpm));

        if (@new is null)
            IsUseInputFile = true;
    }

    [RelayCommand]
    private async Task OpenSelectInputFileAsync()
    {
        var path = await FileDialogHelper.OpenFileAsync(Lang.SelectAudioFile, WavFileFilter);
        if (string.IsNullOrWhiteSpace(path))
            return;

        InputFumenFilePath = path;
        IsUseInputFile = true;
    }

    [RelayCommand]
    private async Task OpenSelectOutputFileAsync()
    {
        var path = await FileDialogHelper.SaveFileAsync(Lang.SaveNewAudioFile, WavFileFilter);
        if (!string.IsNullOrWhiteSpace(path))
            OutputFumenFilePath = path;
    }

    [RelayCommand]
    private async Task ExecuteConverterAsync()
    {
        var currentEditor = editorDocumentManager.CurrentActivatedEditor;
        var audioFilePath = IsUseInputFile
            ? InputFumenFilePath
            : currentEditor?.EditorProjectData?.AudioFilePath;

        if (IsUseInputFile && !File.Exists(InputFumenFilePath))
        {
            await ShowMessageAsync(Lang.ErrorProcessFumenFileNotSelect, DialogMessageType.Error);
            return;
        }

        if (!File.Exists(audioFilePath))
        {
            await ShowMessageAsync(Lang.ErrorProcessAudioNotFound, DialogMessageType.Error);
            return;
        }

        if (string.IsNullOrWhiteSpace(OutputFumenFilePath))
        {
            await ShowMessageAsync(Lang.ErrorSaveAudioFileNotSelect, DialogMessageType.Error);
            return;
        }

        var timeOffset = IsUseGridOffset
            ? TimeSpan.FromMilliseconds(MathUtils.CalculateBPMLength(
                new BPMChange { BPM = Bpm, TGrid = TGrid.Zero },
                TGrid.Zero + new GridOffset(Unit, Grid)))
            : TimeSpan.FromSeconds(Seconds);

        Dictionary<ITimelineObject, (TGrid before, TGrid after)> recalculateMap = null;
        if (IsCurrentEditorAsInputFumen && IsRecalculateObjects && currentEditor is not null)
        {
            var firstBpm = currentEditor.Fumen.BpmList.FirstOrDefault();
            var offset = firstBpm.LengthConvertToOffset(timeOffset.TotalMilliseconds);
            recalculateMap = [];

            foreach (var timelineObject in currentEditor.Fumen.GetAllDisplayableObjects().OfType<ITimelineObject>())
            {
                var newTGrid = timelineObject.TGrid + offset;
                if (newTGrid is null)
                {
                    await ShowMessageAsync($"{Lang.ErrorCantApplyNewAdjust}{timelineObject}", DialogMessageType.Error);
                    return;
                }

                recalculateMap[timelineObject] = (timelineObject.TGrid, newTGrid);
            }
        }

        var result = await OffsetAudioFile(audioFilePath, OutputFumenFilePath, timeOffset);
        if (!result.isSuccess)
        {
            await ShowMessageAsync($"{Lang.ApplyAudioAdjustFail}{result.msg}", DialogMessageType.Error);
            return;
        }

        if (recalculateMap is not null)
        {
            currentEditor.UndoRedoManager.ExecuteAction(LambdaUndoAction.Create(
                Lang.B.ApplyAudioAdjust.ToLocalizedString(),
                () =>
                {
                    foreach (var item in recalculateMap)
                        item.Key.TGrid = item.Value.after.CopyNew();
                },
                () =>
                {
                    foreach (var item in recalculateMap)
                        item.Key.TGrid = item.Value.before.CopyNew();
                }));
        }

        await ShowMessageAsync(IsCurrentEditorAsInputFumen
            ? Lang.ApplyAudioAdjustSuccessButSuggest
            : Lang.ApplyAudioAdjustSuccess);
    }

    private static Task ShowMessageAsync(string message, DialogMessageType messageType = DialogMessageType.Info)
    {
        return IoC.Get<IDialogManager>().ShowMessageDialog(message, messageType);
    }

    public Task<(bool isSuccess, string msg)> OffsetAudioFile(string inputWavFilePath, string saveWavFilePath, TimeSpan offset)
    {
        return Task.Run<(bool isSuccess, string msg)>(() =>
        {
            try
            {
                if (!File.Exists(inputWavFilePath))
                    return (false, $"Input audio file not found: {inputWavFilePath}");

                var outputDirectory = Path.GetDirectoryName(saveWavFilePath);
                if (!string.IsNullOrWhiteSpace(outputDirectory))
                    Directory.CreateDirectory(outputDirectory);

                // Temporary migration fallback: keep original bytes when no offset is requested.
                if (offset == TimeSpan.Zero)
                {
                    File.Copy(inputWavFilePath, saveWavFilePath, overwrite: true);
                    return (true, string.Empty);
                }

                return (false, "Audio offset is not implemented in Avalonia migration yet.");
            }
            catch (Exception e)
            {
                return (false, e.Message);
            }
        });
    }
}
