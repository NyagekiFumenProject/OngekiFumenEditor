using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Gekimini.Avalonia.Framework.Languages;
using Gekimini.Avalonia.Framework.Tools;
using Gekimini.Avalonia.Utils.MethodExtensions;
using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Assets.Languages;
using OngekiFumenEditor.Avalonia.Base;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects;
using OngekiFumenEditor.Avalonia.Modules.FumenObjectPropertyBrowser;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Kernel;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.ViewModels;

namespace OngekiFumenEditor.Avalonia.Modules.FumenTimeSignatureListViewer.ViewModels;

[RegisterSingleton<IFumenTimeSignatureListViewer>]
public partial class FumenTimeSignatureListViewerViewModel : ToolViewModelBase, IFumenTimeSignatureListViewer
{
    public ObservableCollection<DisplayTimeSignatureItem> DisplayTimeSignatures { get; } = [];


    public FumenVisualEditorViewModel Editor
    {
        get => field;
        set
        {
            this.RegisterOrUnregisterPropertyChangeEvent(field, value, OnEditorPropertyChanged);
            if (SetProperty(ref field, value))
                Fumen = value?.EditorContext?.Fumen;
        }
    }

    public OngekiFumen Fumen
    {
        get => field;
        set
        {
            if (field is not null)
            {
                field.BpmList.OnChangedEvent -= OnTimeSignatureListChanged;
                field.MeterChanges.OnChangedEvent -= OnTimeSignatureListChanged;
            }

            if (value is not null)
            {
                value.BpmList.OnChangedEvent += OnTimeSignatureListChanged;
                value.MeterChanges.OnChangedEvent += OnTimeSignatureListChanged;
            }

            if (SetProperty(ref field, value))
            {
                Log.LogDebug("Refresh time signatures list viewer by fumen object changed.");
                RefreshFumen();
            }
        }
    }

    public DisplayTimeSignatureItem CurrentSelectTimeSignature
    {
        get => field;
        set => SetProperty(ref field, value);
    }

    public FumenTimeSignatureListViewerViewModel() : base(Lang.B.FumenTimeSignatureListViewer.ToLocalizedString())
    {

        Dock = global::Dock.Model.Core.DockMode.Bottom;
        IoC.Get<IEditorDocumentManager>().OnActivateEditorChanged += (@new, _) => Editor = @new;
        Editor = IoC.Get<IEditorDocumentManager>().CurrentActivatedEditor;
    }

    private void OnTimeSignatureListChanged()
    {
        Log.LogDebug("Refresh time signatures list viewer.");
        RefreshFumen();
    }

    private void OnEditorPropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(FumenVisualEditorViewModel.EditorContext))
            Fumen = Editor?.EditorContext?.Fumen;
    }

    private void RefreshFumen()
    {
        DisplayTimeSignatures.Clear();
        CurrentSelectTimeSignature = null;
        if (Editor is null || Fumen is null)
            return;

        foreach (var timeSignature in Fumen.MeterChanges.GetCachedAllTimeSignatureUniformPositionList(Fumen.BpmList))
        {
            DisplayTimeSignatures.Add(new DisplayTimeSignatureItem
            {
                StartAudioTime = timeSignature.audioTime,
                BPMChange = timeSignature.bpm,
                Meter = timeSignature.meter,
                StartTGrid = timeSignature.startTGrid
            });
        }
    }

    public void OnItemSingleClick(DisplayTimeSignatureItem item)
    {
        if (item is null || Editor is null)
            return;

        OngekiObjectBase obj = item.StartTGrid == item.BPMChange.TGrid ? item.BPMChange : item.Meter;

        /*
        Editor.SelectObjects.Where(x => x != obj).ForEach(x => x.IsSelected = false);
        if (obj is ISelectableObject selectable)
            selectable.IsSelected = true;
        */
        IoC.Get<IFumenObjectPropertyBrowser>().RefreshSelected(Editor, obj);
    }

    [RelayCommand]
    private void NavigateToTimeSignature(DisplayTimeSignatureItem item)
    {
        Log.LogInfo("NavigateToTimeSignature triggered.");
        if (item is null || Editor is null)
            return;

        Editor.ScrollTo(item.StartTGrid);
        IoC.Get<IFumenObjectPropertyBrowser>().RefreshSelected(Editor);
    }
}
