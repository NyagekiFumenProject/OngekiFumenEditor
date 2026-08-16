using System.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Gekimini.Avalonia.Framework.Languages;
using Gekimini.Avalonia.Framework.Tools;
using Gekimini.Avalonia.Utils.MethodExtensions;
using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Assets.Languages;
using OngekiFumenEditor.Avalonia.Base;
using OngekiFumenEditor.Avalonia.Base.Collections;
using OngekiFumenEditor.Avalonia.Base.EditorObjects;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Kernel;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.ViewModels;
using OngekiFumenEditor.Avalonia.UI.Behaviors;
using static OngekiFumenEditor.Avalonia.Base.Collections.SoflanList;

namespace OngekiFumenEditor.Avalonia.Modules.FumenSoflanGroupListViewer.ViewModels;

[RegisterSingleton<IFumenSoflanGroupListViewer>]
public partial class FumenSoflanGroupListViewerViewModel : ToolViewModelBase, IFumenSoflanGroupListViewer,
    IDataGridRowReorderHandler<SoflanGroupDisplayItemListViewBase>
{
    private OngekiFumen previousFumen;

    public bool IsShowPreviewModeSoflanPositionList
    {
        get => field;
        set
        {
            if (SetProperty(ref field, value))
                OnPropertyChanged(nameof(DisplaySoflanPointList));
        }
    } = true;

    public IEnumerable<SoflanPointRow> DisplaySoflanPointList
    {
        get
        {
            if (CurrentSelectedSoflanGroupWrapItem is null || Editor?.EditorContext?.Fumen is not OngekiFumen fumen)
                return [];

            var soflanList = fumen.SoflansMap[CurrentSelectedSoflanGroupWrapItem.SoflanGroupId];
            var points = IsShowPreviewModeSoflanPositionList
                ? soflanList.GetCachedSoflanPositionList_PreviewMode(fumen.BpmList)
                : soflanList.GetCachedSoflanPositionList_DesignMode(fumen.BpmList);
            return points.Select(point => new SoflanPointRow(point));
        }
    }

    public string CreateNewGroupName
    {
        get => field;
        set
        {
            if (SetProperty(ref field, value))
                CreateNewGroupCommand.NotifyCanExecuteChanged();
        }
    } = string.Empty;

    public SoflanGroupWrapItem CurrentSelectedSoflanGroupWrapItem
    {
        get => field;
        set
        {
            if (SetProperty(ref field, value))
                OnPropertyChanged(nameof(DisplaySoflanPointList));
        }
    }

    public SoflanGroupWrapItem CurrentSoflansDisplaySoflanGroupWrapItem
    {
        get => field;
        set => SetProperty(ref field, value);
    }

    public SoflanGroupWrapItemGroup DisplaySoflanGroupItemGroupRoot
    {
        get => field;
        set
        {
            if (SetProperty(ref field, value))
                CreateNewGroupCommand.NotifyCanExecuteChanged();
        }
    }

    public FumenVisualEditorViewModel Editor
    {
        get => field;
        set
        {
            this.RegisterOrUnregisterPropertyChangeEvent(field, value, OnEditorPropertyChanged);
            if (!SetProperty(ref field, value))
                return;

            RebuildItemGroupRoot();
            RegisterFumenSoflanListMapEvent();
        }
    }

    public FumenSoflanGroupListViewerViewModel() : base(Lang.B.SoflanGroupListViewer.ToLocalizedString())
    {
        Dock = global::Dock.Model.Core.DockMode.Bottom;
        IoC.Get<IEditorDocumentManager>().OnActivateEditorChanged += OnActivateEditorChanged;
        Editor = IoC.Get<IEditorDocumentManager>().CurrentActivatedEditor;
    }

    private void OnActivateEditorChanged(FumenVisualEditorViewModel @new, FumenVisualEditorViewModel old)
    {
        Editor = @new;
    }

    private void OnEditorPropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(FumenVisualEditorViewModel.EditorContext))
        {
            RebuildItemGroupRoot();
            RegisterFumenSoflanListMapEvent();
        }
    }

    private void RegisterFumenSoflanListMapEvent()
    {
        if (previousFumen is not null)
        {
            previousFumen.IndividualSoflanAreaMap.PropertyChanged -= OnIndividualSoflanAreaMapPropertyChanged;
            previousFumen = null;
        }

        if (Editor?.EditorContext?.Fumen is OngekiFumen currentFumen)
        {
            currentFumen.IndividualSoflanAreaMap.PropertyChanged += OnIndividualSoflanAreaMapPropertyChanged;
            previousFumen = currentFumen;
        }
    }

    private void OnIndividualSoflanAreaMapPropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(IndividualSoflanAreaListMap.SoflanGroupWrapItemGroupRoot))
            RebuildItemGroupRoot();
    }

    private bool CanCreateNewGroup() =>
        !string.IsNullOrWhiteSpace(CreateNewGroupName) && DisplaySoflanGroupItemGroupRoot is not null;

    [RelayCommand(CanExecute = nameof(CanCreateNewGroup))]
    private void CreateNewGroup()
    {
        if (string.IsNullOrWhiteSpace(CreateNewGroupName))
        {
            //todo messagebox
            return;
        }
        if (DisplaySoflanGroupItemGroupRoot is null)
        {
            //todo messagebox
            return;
        }

        DisplaySoflanGroupItemGroupRoot.Add(new SoflanGroupWrapItemGroup
        {
            DisplayName = CreateNewGroupName
        });
        CreateNewGroupName = string.Empty;
    }

    public void OnDisplaySoflanItemChecked(SoflanGroupWrapItem item)
    {
        if (!item.IsDisplaySoflanDesignMode)
            throw new Exception("IsDisplaySoflanDesignMode is false.");

        CurrentSoflansDisplaySoflanGroupWrapItem = item;
        Log.LogInfo($"CurrentSoflansDisplaySoflanGroupWrapItem changed: {CurrentSoflansDisplaySoflanGroupWrapItem}");
    }

    public void OnItemChecked(SoflanGroupWrapItem item)
    {
        if (!item.IsSelected)
            throw new Exception("IsSelected is false.");

        CurrentSelectedSoflanGroupWrapItem = item;
        Log.LogInfo($"CurrentSelectedSoflanGroupWrapItem changed: {CurrentSelectedSoflanGroupWrapItem}");
    }

    public bool CanStartReorder(IReadOnlyList<SoflanGroupDisplayItemListViewBase> items)
    {
        return items.Count > 0 &&
               items.All(x => x is SoflanGroupWrapItem { Parent: not null });
    }

    public bool CanReorder(
        IReadOnlyList<SoflanGroupDisplayItemListViewBase> items,
        SoflanGroupDisplayItemListViewBase target,
        DataGridRowDropPosition position)
    {
        return TryCreateGroupReorder(items, target, position, out _, out _);
    }

    public void Reorder(
        IReadOnlyList<SoflanGroupDisplayItemListViewBase> items,
        SoflanGroupDisplayItemListViewBase target,
        DataGridRowDropPosition position)
    {
        if (!TryCreateGroupReorder(items, target, position, out var before, out var after))
            return;

        var action = LambdaUndoAction.Create(
            "Reorder Soflan groups".ToLocalizedStringByRawText(),
            () => ApplyGroupOrder(after),
            () => ApplyGroupOrder(before));

        if (Editor is not null)
            Editor.UndoRedoManager.ExecuteAction(action);
        else
            action.Execute();
    }

    private bool TryCreateGroupReorder(
        IReadOnlyList<SoflanGroupDisplayItemListViewBase> items,
        SoflanGroupDisplayItemListViewBase target,
        DataGridRowDropPosition position,
        out IReadOnlyDictionary<SoflanGroupWrapItemGroup, IReadOnlyList<SoflanGroupDisplayItemListViewBase>> before,
        out IReadOnlyDictionary<SoflanGroupWrapItemGroup, IReadOnlyList<SoflanGroupDisplayItemListViewBase>> after)
    {
        before = null;
        after = null;
        if (!CanStartReorder(items) || target is null || DisplaySoflanGroupItemGroupRoot is null)
            return false;

        SoflanGroupWrapItemGroup destination;
        int insertionBoundary;
        switch (target)
        {
            case SoflanGroupWrapItemGroup group when position == DataGridRowDropPosition.Inside:
                destination = group;
                insertionBoundary = group.Children.Count;
                break;
            case SoflanGroupWrapItem item when
                position != DataGridRowDropPosition.Inside && item.Parent is not null:
                destination = item.Parent;
                insertionBoundary = destination.IndexOf(item) +
                    (position == DataGridRowDropPosition.After ? 1 : 0);
                break;
            default:
                return false;
        }

        var movingSet = items.OfType<SoflanGroupWrapItem>().ToHashSet();
        var orderedMovingItems = DisplaySoflanGroupItemGroupRoot.DisplayableItemSource
            .OfType<SoflanGroupWrapItem>()
            .Where(movingSet.Contains)
            .Cast<SoflanGroupDisplayItemListViewBase>()
            .ToArray();
        if (orderedMovingItems.Length != movingSet.Count)
            return false;

        var affectedGroups = orderedMovingItems
            .Select(x => x.Parent)
            .Append(destination)
            .Distinct()
            .ToArray();
        var beforeSnapshot = affectedGroups.ToDictionary(
            x => x,
            x => (IReadOnlyList<SoflanGroupDisplayItemListViewBase>)x.Children.ToArray());
        var afterLists = beforeSnapshot.ToDictionary(
            x => x.Key,
            x => x.Value.ToList());

        var removedBeforeBoundary = beforeSnapshot[destination]
            .Take(insertionBoundary)
            .Count(x => movingSet.Contains(x as SoflanGroupWrapItem));
        foreach (var list in afterLists.Values)
            list.RemoveAll(x => movingSet.Contains(x as SoflanGroupWrapItem));

        var insertionIndex = insertionBoundary - removedBeforeBoundary;
        afterLists[destination].InsertRange(insertionIndex, orderedMovingItems);

        var changed = affectedGroups.Any(group =>
            !beforeSnapshot[group].SequenceEqual(afterLists[group]));
        if (!changed)
            return false;

        before = beforeSnapshot;
        after = afterLists.ToDictionary(
            x => x.Key,
            x => (IReadOnlyList<SoflanGroupDisplayItemListViewBase>)x.Value.ToArray());
        return true;
    }

    private static void ApplyGroupOrder(
        IReadOnlyDictionary<SoflanGroupWrapItemGroup, IReadOnlyList<SoflanGroupDisplayItemListViewBase>> snapshot)
    {
        foreach (var (group, desiredItems) in snapshot)
        {
            for (var i = 0; i < desiredItems.Count; i++)
            {
                var item = desiredItems[i];
                if (item.Parent != group)
                {
                    item.Parent?.Remove(item);
                    group.Insert(i, item);
                }
                else
                {
                    group.Move(item, i);
                }
            }
        }
    }

    private void RebuildItemGroupRoot()
    {
        DisplaySoflanGroupItemGroupRoot = null;
        CurrentSelectedSoflanGroupWrapItem = null;
        CurrentSoflansDisplaySoflanGroupWrapItem = null;

        if (Editor?.EditorContext?.Fumen is not OngekiFumen fumen)
            return;

        DisplaySoflanGroupItemGroupRoot = fumen.IndividualSoflanAreaMap.SoflanGroupWrapItemGroupRoot;

        static IEnumerable<SoflanGroupDisplayItemListViewBase> Visit(SoflanGroupDisplayItemListViewBase item)
        {
            yield return item;
            if (item is not SoflanGroupWrapItemGroup group)
                yield break;

            foreach (var child in group.Children)
            {
                foreach (var subItem in Visit(child))
                    yield return subItem;
            }
        }

        foreach (var item in Visit(DisplaySoflanGroupItemGroupRoot).OfType<SoflanGroupWrapItem>())
        {
            if (item.IsSelected)
                CurrentSelectedSoflanGroupWrapItem = item;
            if (item.IsDisplaySoflanDesignMode)
                CurrentSoflansDisplaySoflanGroupWrapItem = item;
        }
    }

    [RelayCommand]
    private void NavigateToSoflanPoint(SoflanPointRow item)
    {
        Editor?.ScrollTo(item.TGrid);
    }
}

public sealed class SoflanPointRow(SoflanPoint point)
{
    public double Y { get; } = point.Y;
    public TGrid TGrid { get; } = point.TGrid;
    public double Bpm { get; } = point.Bpm.BPM;
    public double Speed { get; } = point.Speed;
}
