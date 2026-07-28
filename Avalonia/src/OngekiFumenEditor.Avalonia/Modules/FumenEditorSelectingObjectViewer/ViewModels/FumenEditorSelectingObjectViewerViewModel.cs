using Dock.Model.Core;
using Gekimini.Avalonia.Framework.Languages;
using Gekimini.Avalonia.Framework.Tools;
using Gekimini.Avalonia.Utils.MethodExtensions;
using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Base;
using OngekiFumenEditor.Avalonia.Modules.FumenMetaInfoBrowser;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Kernel;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.ViewModels;
using OngekiFumenEditor.Avalonia.Utils;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace OngekiFumenEditor.Avalonia.Modules.FumenEditorSelectingObjectViewer.ViewModels;

[RegisterSingleton<IFumenMetaInfoBrowser>]
public class FumenEditorSelectingObjectViewerViewModel : ToolViewModelBase, IFumenMetaInfoBrowser
{
    private IEditorDocumentManager EditorDocumentManager => OngekiFumenEditor.Avalonia.Avalonia.IoC.Get<IEditorDocumentManager>();

    public ObservableCollection<ISelectableObject> SelectedItems { get; } = [];
    public ObservableCollection<ISelectableObject> EditorSelectObjects { get; } = [];

    public SelectionFilterViewModel SelectionFilter { get; }

    public FumenVisualEditorViewModel Editor
    {
        get => field;
        set
        {
            this.RegisterOrUnregisterPropertyChangeEvent(field, value, OnEditorPropChanged);
            if (SetProperty(ref field, value))
                OnRefresh();
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

    public FumenEditorSelectingObjectViewerViewModel() : base("Selecting Objects".ToLocalizedStringByRawText())
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
            OnRefresh();
    }

    public void OnRefresh()
    {
        EditorSelectObjects.Clear();
        foreach (var item in Editor?.SelectObjects ?? [])
            EditorSelectObjects.Add(item);

        if (IsFilterMenuVisible)
            SelectionFilter.OnSelectedItemsRefreshed();
    }

    public void OnCancelItemSelectedObjects()
    {
        foreach (var item in SelectedItems.ToArray())
            item.IsSelected = false;
    }
}
