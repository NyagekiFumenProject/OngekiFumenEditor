using Dock.Model.Core;
using Avalonia.Collections;
using CommunityToolkit.Mvvm.Input;
using Gekimini.Avalonia.Framework.Languages;
using Gekimini.Avalonia.Framework.Tools;
using Gekimini.Avalonia.Utils.MethodExtensions;
using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Assets.Languages;
using OngekiFumenEditor.Avalonia.Base;
using OngekiFumenEditor.Avalonia.Modules.FumenObjectPropertyBrowser;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Kernel;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.ViewModels;
using OngekiFumenEditor.Avalonia.Utils;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace OngekiFumenEditor.Avalonia.Modules.FumenEditorSelectingObjectViewer.ViewModels;

[RegisterSingleton<IFumenEditorSelectingObjectViewer>]
public partial class FumenEditorSelectingObjectViewerViewModel : ToolViewModelBase, IFumenEditorSelectingObjectViewer
{
    private IEditorDocumentManager EditorDocumentManager => OngekiFumenEditor.Avalonia.IoC.Get<IEditorDocumentManager>();

    public ObservableCollection<SelectedObjectRow> SelectedItems { get; } = [];
    private readonly ObservableCollection<SelectedObjectRow> editorSelectObjectSource = [];
    private DataGridCollectionView editorSelectObjects;
    public DataGridCollectionView EditorSelectObjects =>
        editorSelectObjects ??= new DataGridCollectionView(editorSelectObjectSource);

    public SelectionFilterViewModel SelectionFilter { get; }

    public FumenVisualEditorViewModel Editor
    {
        get => field;
        set
        {
            this.RegisterOrUnregisterPropertyChangeEvent(field, value, OnEditorPropChanged);
            if (SetProperty(ref field, value))
                Refresh();
        }
    }

    public bool IsFilterMenuVisible
    {
        get => field;
        set
        {
            if (SetProperty(ref field, value) && value)
                SelectionFilter.OnSelectedItemsRefreshed();
        }
    }

    public FumenEditorSelectingObjectViewerViewModel() : base(Lang.B.FumenEditorSelectingObjectViewer.ToLocalizedString())
    {
        Dock = DockMode.Right;
        SelectionFilter = new SelectionFilterViewModel(this);

        EditorDocumentManager.OnActivateEditorChanged += OnActivateEditorChanged;
        Editor = EditorDocumentManager.CurrentActivatedEditor;
    }

    private void OnActivateEditorChanged(FumenVisualEditorViewModel @new, FumenVisualEditorViewModel old)
    {
        Editor = @new;
    }

    private void OnEditorPropChanged(object sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(FumenVisualEditorViewModel.SelectObjects))
            Refresh();
    }

    [RelayCommand]
    private void Refresh()
    {
        var selectedObjects = SelectedItems
            .Select(x => x.Object)
            .ToHashSet(ReferenceEqualityComparer.Instance);

        editorSelectObjectSource.Clear();
        SelectedItems.Clear();
        foreach (var item in Editor?.SelectObjects ?? [])
        {
            var row = new SelectedObjectRow(item, Editor.Fumen);
            editorSelectObjectSource.Add(row);
            if (selectedObjects.Contains(item))
                SelectedItems.Add(row);
        }

        if (IsFilterMenuVisible)
            SelectionFilter.OnSelectedItemsRefreshed();
    }

    [RelayCommand]
    private void CancelSelectedObjects()
    {
        foreach (var item in SelectedItems.ToArray())
            item.Object.IsSelected = false;

        IoC.Get<IFumenObjectPropertyBrowser>().RefreshSelected(Editor);
    }

    public void OnItemSingleClick(ISelectableObject item)
    {
        if (Editor is null || item is null)
            return;

        IoC.Get<IFumenObjectPropertyBrowser>().RefreshSelected(Editor, item);
    }

    [RelayCommand]
    private void FocusItem(ISelectableObject item)
    {
        if (Editor is null || item is null)
            return;

        if (item is ITimelineObject timelineObject)
            Editor.ScrollTo(timelineObject.TGrid);

        foreach (var selected in Editor.SelectObjects.Where(x => x != item).ToArray())
            selected.IsSelected = false;

        IoC.Get<IFumenObjectPropertyBrowser>().RefreshSelected(Editor);
    }
}

public sealed class SelectedObjectRow
{
    public ISelectableObject Object { get; }
    public string Name { get; }
    public TGrid TGrid { get; }
    public int? SoflanGroup { get; }
    public string Description { get; }

    public SelectedObjectRow(ISelectableObject selectableObject, OngekiFumen fumen = null)
    {
        Object = selectableObject;
        Name = Object is OngekiObjectBase ongekiObject
            ? ongekiObject.Name
            : Object.GetType().Name;
        TGrid = (Object as ITimelineObject)?.TGrid;
        SoflanGroup = ResolveSoflanGroup(Object, fumen);
        Description = Object.ToString() ?? string.Empty;
    }

    private static int? ResolveSoflanGroup(ISelectableObject selectableObject, OngekiFumen fumen)
    {
        if (fumen is null ||
            selectableObject is not ITimelineObject { TGrid: { } tGrid } ||
            selectableObject is not IHorizonPositionObject { XGrid: { } xGrid })
            return null;

        return fumen.IndividualSoflanAreaMap.QuerySoflanGroup(xGrid, tGrid);
    }
}
