using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text.RegularExpressions;
using Caliburn.Micro;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.EditorObjects.LaneCurve;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Utils;

namespace OngekiFumenEditor.Modules.SelectionFilter.ViewModels;

public sealed class SelectionFilterOptionsViewModel : PropertyChangedBase
{
    private readonly SelectionFilterViewModel SelectionFilter;

    public delegate void OptionChangedEventHandler();
    public event OptionChangedEventHandler OptionChanged;

    private FilterMode _filterSelectionChangeMode = FilterMode.Replace;
    private YesNoFilteringMode _isCriticalFilterMode = YesNoFilteringMode.Any;
    private LinePointTypeFilteringMode _nodeTypeFilteringMode = LinePointTypeFilteringMode.Any;
    private LinePointTypeFilteringMode _holdPartFilteringMode = LinePointTypeFilteringMode.Any;
    private string _tagFilter = string.Empty;
    private bool _isTagFilterRegex = false;
    private bool _onlyMatchLanesWithSelectedStartNodes = false;
    private bool _onlyMatchCurvesWithSelectedLaneNodes = false;
    private bool _onlyMatchHoldsEndsWithSelectedStartNodes = false;
    private LeftRightFilteringMode _flickDirectionFilteringMode = LeftRightFilteringMode.Any;
    private LeftRightFilteringMode _laneBlockDirectionFilteringMode = LeftRightFilteringMode.Any;

    public ObservableCollection<FilterBulletPalettesItem> FilterBulletPalettesItems { get; } = new();

    public ObservableCollection<FilterDockableLaneOption> FilterDockableLaneOptions { get; } = new()
    {
        new() { Name = "WallLeft", Filter = d => d.ReferenceLaneStart.LaneType == LaneType.WallLeft},
        new() { Name = "LaneLeft", Filter = d => d.ReferenceLaneStart.LaneType == LaneType.Left},
        new() { Name = "LaneCenter", Filter = d => d.ReferenceLaneStart.LaneType == LaneType.Center},
        new() { Name = "LaneRight", Filter = d => d.ReferenceLaneStart.LaneType == LaneType.Right},
        new() { Name = "WallRight", Filter = d => d.ReferenceLaneStart.LaneType == LaneType.WallRight},
        new() { Name = "Undocked", Filter = d => d.ReferenceLaneStart == null},
    };

    public FilterMode FilterSelectionChangeMode
    {
        get => _filterSelectionChangeMode;
        set => Set(ref _filterSelectionChangeMode, value);
    }

    public YesNoFilteringMode IsCriticalFilterMode
    {
        get => _isCriticalFilterMode;
        set => Set(ref _isCriticalFilterMode, value);
    }

    public LinePointTypeFilteringMode NodeTypeFilteringMode
    {
        get => _nodeTypeFilteringMode;
        set => Set(ref _nodeTypeFilteringMode, value);
    }

    public LinePointTypeFilteringMode HoldPartFilteringMode
    {
        get => _holdPartFilteringMode;
        set => Set(ref _holdPartFilteringMode, value);
    }

    public string TagFilter
    {
        get => _tagFilter;
        set => Set(ref _tagFilter, value);
    }

    public bool IsTagFilterRegex
    {
        get => _isTagFilterRegex;
        set => Set(ref _isTagFilterRegex, value);
    }


    public bool OnlyMatchLanesWithSelectedStartNodes
    {
        get => _onlyMatchLanesWithSelectedStartNodes;
        set => Set(ref _onlyMatchLanesWithSelectedStartNodes, value);
    }

    public bool OnlyMatchHoldsEndsWithSelectedStartNodes
    {
        get => _onlyMatchHoldsEndsWithSelectedStartNodes;
        set => Set(ref _onlyMatchHoldsEndsWithSelectedStartNodes, value);
    }

    public LeftRightFilteringMode FlickDirectionFilteringMode
    {
        get => _flickDirectionFilteringMode;
        set => Set(ref _flickDirectionFilteringMode, value);
    }

    public bool OnlyMatchCurvesWithSelectedLaneNodes
    {
        get => _onlyMatchCurvesWithSelectedLaneNodes;
        set => Set(ref _onlyMatchCurvesWithSelectedLaneNodes, value);
    }

    public LeftRightFilteringMode LaneBlockDirectionFilteringMode
    {
        get => _laneBlockDirectionFilteringMode;
        set => Set(ref _laneBlockDirectionFilteringMode, value);
    }

    public SelectionFilterOptionsViewModel(SelectionFilterViewModel selectionFilter)
    {
        SelectionFilter = selectionFilter;

        FilterBulletPalettesItems.AddRange(selectionFilter.Editor.Fumen.BulletPalleteList.Select(bpl => new FilterBulletPalettesItem(bpl)));

        // Set up OptionChanged events

        PropertyChanged += (_, args) =>
        {
            OptionChanged?.Invoke();
        };

        FilterDockableLaneOptions.ForEach(x =>
        {
            x.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(FilterDockableLaneOption.IsSelected)) {
                    OptionChanged?.Invoke();
                }
            };
        });

        FilterBulletPalettesItems.ForEach(i => i.PropertyChanged += (sender, args) =>
        {
            OptionChanged?.Invoke();
        });

        // Initialize bullet palletes list
        if (selectionFilter.Editor is not null) {
            foreach (var bullets in selectionFilter.Editor.Fumen.Bullets
                         .Concat<IBulletPalleteReferencable>(selectionFilter.Editor.Fumen.Bells)
                         .Where(b => b.ReferenceBulletPallete is not null)
                         .GroupBy(b => b.ReferenceBulletPallete)) {
                var filterItem = FilterBulletPalettesItems.First(b => b.BulletPalette == bullets.Key);
                filterItem.MatchingBullets.AddRange(bullets.OfType<Bullet>());
                filterItem.MatchingBells.AddRange(bullets.OfType<Bell>());
            }
        }
    }

    public bool IsMatch(ISelectableObject obj)
    {
        if (IsCriticalFilterMode != YesNoFilteringMode.Any && obj is ICriticalableObject criticalObj)
            switch (criticalObj.IsCritical) {
                case true when IsCriticalFilterMode == YesNoFilteringMode.No:
                case false when IsCriticalFilterMode == YesNoFilteringMode.Yes:
                    return false;
            }


        if (NodeTypeFilteringMode != LinePointTypeFilteringMode.Any && obj is ConnectableObjectBase connectableObj)
            switch (connectableObj) {
                case ConnectableChildObjectBase when NodeTypeFilteringMode == LinePointTypeFilteringMode.Head:
                case ConnectableStartObject when NodeTypeFilteringMode == LinePointTypeFilteringMode.Tail:
                    return false;
            }


        if (HoldPartFilteringMode != LinePointTypeFilteringMode.Any && obj is Hold or HoldEnd)
            switch (obj) {
                case HoldEnd when HoldPartFilteringMode == LinePointTypeFilteringMode.Head:
                case Hold when HoldPartFilteringMode == LinePointTypeFilteringMode.Tail:
                    return false;
            }


        if (TagFilter != string.Empty) {
            var tag = ((OngekiObjectBase)obj).Tag;
            if (IsTagFilterRegex) {
                if (!Regex.IsMatch(tag, TagFilter))
                    return false;
            }
            else if (tag != TagFilter) {
                return false;
            }
        }

        if (obj is ILaneDockable dockable && FilterDockableLaneOptions.Any(o => o.IsSelected && !o.Filter(dockable))) {
            return false;
        }

        if (obj is Flick flick && FlickDirectionFilteringMode != LeftRightFilteringMode.Any) {
            if (FlickDirectionFilteringMode.ToFlickDirection() != flick.Direction) {
                return false;
            }
        }

        if (obj is IBulletPalleteReferencable bplObj
            && FilterBulletPalettesItems.Any(f => f.IsSelected)
            && FilterBulletPalettesItems.Any(f => !f.IsSelected && bplObj.ReferenceBulletPallete == f.BulletPalette)) {
            return false;
        }

        if (OnlyMatchLanesWithSelectedStartNodes && obj is ConnectableChildObjectBase laneNext) {
            if (!SelectionFilter.Editor.SelectObjects.Contains(laneNext.ReferenceStartObject)) {
                return false;
            }
        }

        if (OnlyMatchLanesWithSelectedStartNodes && obj is HoldEnd holdEnd) {
            if (!SelectionFilter.Editor.SelectObjects.Contains(holdEnd.ReferenceLaneStart)) {
                return false;
            }
        }

        if (OnlyMatchCurvesWithSelectedLaneNodes && obj is LaneCurvePathControlObject curve) {
            if (!SelectionFilter.Editor.SelectObjects.Contains(curve.RefCurveObject))
                return false;
        }

        return true;
    }
}
