using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using Gekimini.Avalonia.Framework.Languages;
using Gekimini.Avalonia.Framework.Tools;
using Gekimini.Avalonia.Utils.MethodExtensions;
using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Assets.Languages;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Kernel;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.ViewModels;
using OngekiFumenEditor.Avalonia.UI.Behaviors;

namespace OngekiFumenEditor.Avalonia.Modules.FumenEditorRenderControlViewer.ViewModels;

[RegisterSingleton<IFumenEditorRenderControlViewer>]
public partial class FumenEditorRenderControlViewerViewModel : ToolViewModelBase, IFumenEditorRenderControlViewer,
    IDataGridRowReorderHandler<RenderControlItem>
{
    public FumenVisualEditorViewModel Editor
    {
        get => field;
        set
        {
            if (SetProperty(ref field, value))
                _ = RebuildItemsAsync(false);
        }
    }

    public ObservableCollection<RenderControlItem> ControlItems { get; } = [];


    public FumenEditorRenderControlViewerViewModel() : base(Lang.B.FumenEditorRenderControlViewer.ToLocalizedString())
    {

        Dock = global::Dock.Model.Core.DockMode.Right;
        IoC.Get<IEditorDocumentManager>().OnActivateEditorChanged += OnActivateEditorChanged;
        Editor = IoC.Get<IEditorDocumentManager>().CurrentActivatedEditor;
    }

    private void OnActivateEditorChanged(FumenVisualEditorViewModel @new, FumenVisualEditorViewModel old)
    {
        Editor = @new;
    }

    private async Task RebuildItemsAsync(bool sortByDefault)
    {
        ControlItems.Clear();
        var editor = Editor;
        if (editor is null)
            return;

        await editor.WaitForRenderInitializationIsDone();
        if (editor != Editor)
            return;

        var targets = editor.CurrentDrawingTargets
            .OrderBy(x => sortByDefault ? x.DefaultRenderOrder : x.CurrentRenderOrder)
            .ToArray();
        foreach (var target in targets)
            ControlItems.Add(new RenderControlItem(target));
        UpdateRenderOrder();
    }

    private void UpdateRenderOrder()
    {
        for (var i = 0; i < ControlItems.Count; i++)
            ControlItems[i].RenderOrder = i;
    }

    public bool CanStartReorder(IReadOnlyList<RenderControlItem> items)
    {
        return items.Count > 0;
    }

    public bool CanReorder(
        IReadOnlyList<RenderControlItem> items,
        RenderControlItem target,
        DataGridRowDropPosition position)
    {
        if (items.Count == 0 || target is null || position == DataGridRowDropPosition.Inside)
            return false;

        var reordered = DataGridRowReorderOperations.Reorder(
            ControlItems,
            items,
            target,
            position);
        return !ControlItems.SequenceEqual(reordered);
    }

    public void Reorder(
        IReadOnlyList<RenderControlItem> items,
        RenderControlItem target,
        DataGridRowDropPosition position)
    {
        if (!CanReorder(items, target, position))
            return;

        var before = ControlItems.ToArray();
        var after = DataGridRowReorderOperations.Reorder(
            before,
            items,
            target,
            position).ToArray();
        var action = LambdaUndoAction.Create(
            "Reorder render controls".ToLocalizedStringByRawText(),
            () => ApplyControlItemOrder(after),
            () => ApplyControlItemOrder(before));

        if (Editor is not null)
            Editor.UndoRedoManager.ExecuteAction(action);
        else
            action.Execute();
    }

    private void ApplyControlItemOrder(IReadOnlyList<RenderControlItem> order)
    {
        for (var i = 0; i < order.Count; i++)
        {
            var oldIndex = ControlItems.IndexOf(order[i]);
            if (oldIndex >= 0 && oldIndex != i)
                ControlItems.Move(oldIndex, i);
        }

        UpdateRenderOrder();
    }

    [RelayCommand]
    private async Task ResetDefaultAsync()
    {
        Log.LogInfo("ResetDefault triggered.");

        await RebuildItemsAsync(true);
        foreach (var item in ControlItems)
        {
            item.Target.Visible = item.Target.DefaultVisible;
            item.Refresh();
        }
    }

    [RelayCommand]
    private void Save()
    {
        Log.LogInfo("Save triggered.");
        Editor?.SaveRenderOrderVisible();
    }
}
