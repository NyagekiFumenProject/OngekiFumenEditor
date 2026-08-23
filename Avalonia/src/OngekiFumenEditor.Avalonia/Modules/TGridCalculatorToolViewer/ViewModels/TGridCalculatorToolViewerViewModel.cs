using Dock.Model.Core;
using CommunityToolkit.Mvvm.Input;
using Gekimini.Avalonia.Framework.Languages;
using Gekimini.Avalonia.Framework.Tools;
using Gekimini.Avalonia.Utils.MethodExtensions;
using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Assets.Languages;
using OngekiFumenEditor.Avalonia.Base;
using OngekiFumenEditor.Avalonia.Modules.FumenObjectPropertyBrowser;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Kernel;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.ViewModels;
using OngekiFumenEditor.Avalonia.Utils;
using Microsoft.Extensions.Logging;
using System.ComponentModel;
using System.Globalization;

namespace OngekiFumenEditor.Avalonia.Modules.TGridCalculatorToolViewer.ViewModels;

[RegisterSingleton<ITGridCalculatorToolViewer>]
public partial class TGridCalculatorToolViewerViewModel : ToolViewModelBase, ITGridCalculatorToolViewer
{
    public FumenVisualEditorViewModel Editor
    {
        get => field;
        set
        {
            if (!SetProperty(ref field, value))
                return;

            OnPropertyChanged(nameof(IsEnabled));
            UpdateToTGridCommand.NotifyCanExecuteChanged();
            UpdateToMsecCommand.NotifyCanExecuteChanged();
            ScrollEditorToTGridCommand.NotifyCanExecuteChanged();
        }
    }

    public ITimelineObject TimelineObject
    {
        get => field;
        set
        {
            if (!SetProperty(ref field, value))
                return;

            if (value is not null && IsAutoUpdateTimeIfSelectedObject)
            {
                Unit = value.TGrid.Unit;
                Grid = value.TGrid.Grid;
                UpdateToTGrid();
                UpdateToMsec();
            }

            OnPropertyChanged(nameof(IsSelectedObject));
            ApplyTGridToObjectCommand.NotifyCanExecuteChanged();
        }
    }

    public int Grid
    {
        get => field;
        set
        {
            if (SetProperty(ref field, value))
                UpdateToMsec();
        }
    }

    public float Unit
    {
        get => field;
        set
        {
            if (SetProperty(ref field, value))
                UpdateToMsec();
        }
    }

    private string msecStr = "00:00:00.000";

    public string MsecStr
    {
        get => msecStr;
        set
        {
            if (SetProperty(ref msecStr, value))
                UpdateToTGrid();
        }
    }

    public bool IsAutoUpdateTimeIfSelectedObject { get; set; }
    public bool IsEnabled => Editor is not null;
    public bool IsSelectedObject => TimelineObject is not null;

    private readonly ILogger<TGridCalculatorToolViewerViewModel> logger;

    public TGridCalculatorToolViewerViewModel(ILogger<TGridCalculatorToolViewerViewModel> logger) : base(Lang.B.TGridCalculatorToolViewer.ToLocalizedString())
    {
        this.logger = logger;
        Dock = DockMode.Bottom;
        IoC.Get<IEditorDocumentManager>().OnActivateEditorChanged += OnActivateEditorChanged;
        Editor = IoC.Get<IEditorDocumentManager>().CurrentActivatedEditor;
        IoC.Get<IFumenObjectPropertyBrowser>().PropertyChanged += OnPropertyBrowserPropertyChanged;
    }

    private bool CanUseEditor() => Editor is not null;

    [RelayCommand(CanExecute = nameof(CanUseEditor))]
    private void UpdateToTGrid()
    {
        var audioTime = ParseMsecStr();
        if (Editor is null || audioTime is null)
            return;

        Log.LogInfo($"{MsecStr}  ->  {audioTime}");
        var tGrid = TGridCalculator.ConvertAudioTimeToTGrid(audioTime.Value, Editor);
        Unit = tGrid.Unit;
        Grid = tGrid.Grid;
    }

    [RelayCommand(CanExecute = nameof(CanUseEditor))]
    private void UpdateToMsec()
    {
        if (Editor is null)
            return;

        msecStr = TGridCalculator.ConvertTGridToAudioTime(new TGrid(Unit, Grid), Editor)
            .ToString("hh\\:mm\\:ss\\.fff");
        OnPropertyChanged(nameof(MsecStr));
    }

    private void OnPropertyBrowserPropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(IFumenObjectPropertyBrowser.SelectedObjects))
            return;

        var objects = ((IFumenObjectPropertyBrowser)sender).SelectedObjects;
        TimelineObject = objects.Count == 1 ? objects.OfType<ITimelineObject>().FirstOrDefault() : null;
    }

    private void OnActivateEditorChanged(FumenVisualEditorViewModel @new, FumenVisualEditorViewModel old)
    {
        Editor = @new;
    }

    private TimeSpan? ParseMsecStr()
    {
        // hh:mm:ss.msec
        // 01:05:500.571
        if (TimeSpan.TryParse(MsecStr, CultureInfo.InvariantCulture, out var time))
            return time;
        if (double.TryParse(MsecStr, NumberStyles.Float, CultureInfo.InvariantCulture, out var milliseconds))
            return TimeSpan.FromMilliseconds(milliseconds);
        return null;
    }

    [RelayCommand(CanExecute = nameof(CanUseEditor))]
    private void ScrollEditorToTGrid()
    {
        logger.LogInformation("Scroll editor to TGrid ({Unit}, {Grid}).", Unit, Grid);
        Editor.ScrollTo(new TGrid(Unit, Grid));
    }

    private bool CanApplyTGridToObject() => TimelineObject is not null;

    [RelayCommand(CanExecute = nameof(CanApplyTGridToObject))]
    private void ApplyTGridToObject()
    {
        logger.LogInformation("Apply TGrid ({Unit}, {Grid}) to {ObjectType}.", Unit, Grid, TimelineObject.GetType().Name);
        TimelineObject.TGrid = new TGrid(Unit, Grid);
    }
}
