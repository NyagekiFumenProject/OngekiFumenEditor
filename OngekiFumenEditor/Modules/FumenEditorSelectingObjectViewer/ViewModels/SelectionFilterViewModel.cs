using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Data;
using Caliburn.Micro;
using NAudio.CoreAudioApi;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.EditorObjects;
using OngekiFumenEditor.Base.EditorObjects.LaneCurve;
using OngekiFumenEditor.Base.EditorObjects.Svg;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Base.OngekiObjects.Beam;
using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Base.OngekiObjects.Lane;
using OngekiFumenEditor.Base.OngekiObjects.Wall;
using OngekiFumenEditor.Modules.FumenEditorSelectingObjectViewer.Views;
using OngekiFumenEditor.Modules.FumenVisualEditor.Kernel;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using OngekiFumenEditor.Utils;
using Xv2CoreLib.ACB;
using Binding = System.Windows.Data.Binding;

namespace OngekiFumenEditor.Modules.FumenEditorSelectingObjectViewer.ViewModels;

public class SelectionFilterViewModel : ViewAware
{
    public FumenVisualEditorViewModel Editor { get; private set; }

    public FilterOptions FilterOptions { get; set; }

    public ObservableCollection<ISelectableObject> ObjectTypeFilterMatches { get; } = new();
    public ObservableCollection<ISelectableObject> OptionFilterRemovals { get; } = new();

    private string _fitlerOutcomeText;
    public string FilterOutcomeText
    {
        get => _fitlerOutcomeText;
        private set => Set(ref _fitlerOutcomeText, value);
    }

    // Filter categories

    public ObservableCollection<FilterObjectTypeCategory> FilterCategories { get; set; } = new();

    public SelectionFilterViewModel()
    {
        Editor = IoC.Get<IEditorDocumentManager>().CurrentActivatedEditor;

        InitObjectTypeFilter();

        FilterOptions = new(this);
        FilterOptions.OptionChanged += (sender, args) =>
        {
            // Update filter option matches
            OptionFilterRemovals.Clear();
            OptionFilterRemovals.AddRange(ObjectTypeFilterMatches.Where(o => !FilterOptions.IsMatch(o)));
            UpdateFilterOutcomeText();
        };

        UpdateFilterOutcomeText();
    }

    private void InitObjectTypeFilter()
    {
        FilterCategories.Add(new(this, "Lanes", [

            new(this) { Text = "Wall Left", Types = [typeof(WallLeftNext), typeof(WallLeftStart)] },
            new(this) { Text = "Lane Left", Types = [typeof(LaneLeftNext), typeof(LaneLeftStart)] },
            new(this) { Text = "Lane Center", Types = [typeof(LaneCenterNext), typeof(LaneCenterStart)] },
            new(this) { Text = "Lane Right", Types = [typeof(LaneRightNext), typeof(LaneRightStart)] },
            new(this) { Text = "Wall Right", Types = [typeof(WallRightNext), typeof(WallRightStart)] },
            new(this) { Text = "Lane Colorful", Types = [typeof(ColorfulLaneNext), typeof(ColorfulLaneStart)] },
            new(this) { Text = "AutoPlayFaderLane", Types = [typeof(AutoplayFaderLaneNext), typeof(AutoplayFaderLaneStart)] },
            new(this) { Text = "Beam", Types = [typeof(BeamNext), typeof(BeamStart)] },
            new(this) { Text = "Curve Controls", Types = [typeof(LaneCurvePathControlObject)] }
        ]));

        FilterCategories.Add(new(this, "Dockable", [
            new(this) { Text = "Tap", Types = [typeof(Tap)] },
            new(this) { Text = "Hold", Types = [typeof(Hold), typeof(HoldEnd)] }
        ]));

        FilterCategories.Add(new(this, "Floating", [

            new(this) { Text = "Bell", Types = [typeof(Bell)] },
            new(this) { Text = "Bullet", Types = [typeof(Bullet)] },
            new(this) { Text = "Flick", Types = [typeof(Flick)] }
        ]));

        FilterCategories.Add(new(this, "Timeline", [
            new(this) { Text = "Lane Block", Types = [typeof(LaneBlockArea), typeof(LaneBlockArea.LaneBlockAreaEndIndicator)] },
            new(this) { Text = "Click SE", Types = [typeof(ClickSE)] },
            new(this) { Text = "Interpolatable Soflan", Types = [typeof(InterpolatableSoflan)] },
            new(this) { Text = "Keyframe Soflan", Types = [typeof(KeyframeSoflan)] },
            new(this) { Text = "Duration Soflan", Types = [typeof(IDurationSoflan)] },
            new(this) { Text = "Meter Change", Types = [typeof(MeterChange)] },
            new(this) { Text = "Individual Soflan Area", Types = [typeof(IndividualSoflanArea)] },
        ]));

        FilterCategories.Add(new(this, "Misc.", [

            new(this) { Text = "SVG Prefab (File)", Types = [typeof(SvgImageFilePrefab)] },
            new(this) { Text = "SVG Prefab (Text)", Types = [typeof(SvgStringPrefab)] },
            new(this) { Text = "Comment", Types = [typeof(Comment)] }
        ]));

        // Add selected objects to each category
        if (Editor is not null) {
            foreach (var item in Editor.SelectObjects) {
                var matchingCategory = FilterCategories.SelectMany(c => c.Items)
                    .FirstOrDefault(i => i.Types.Any(t => t.IsInstanceOfType(item)));
                matchingCategory?.MatchingObjects.Add(item);
            }
        }

        foreach (var category in FilterCategories) {
            foreach (var item in category.Items) {
                if (item.MatchingObjects.Count > 0) {
                    item.IsSelected = true;
                }
            }

            category.UpdateCategoryNameDisplay();
        }
    }

    private IEnumerable<ISelectableObject> GetAllFilterMatches()
        => ObjectTypeFilterMatches.Except(OptionFilterRemovals);

    public void FilterObjectTypeSelectedChanged(FilterObjectTypesItem filterType)
    {
        if (filterType.IsSelected) {
            ObjectTypeFilterMatches.AddRange(filterType.MatchingObjects);
        }
        else {
            ObjectTypeFilterMatches.RemoveRange(filterType.MatchingObjects);
        }

        UpdateFilterOutcomeText();
    }

    private void UpdateFilterOutcomeText()
    {
        var matches = GetAllFilterMatches();
        FilterOutcomeText = $"{matches.Count()} objects will be remaining in the selection";
    }
}

public enum FilterMode
{
    Remove,
    Replace
}

public sealed class FilterOptions : PropertyChangedBase
{
    private readonly SelectionFilterViewModel SelectionFilter;

    public event PropertyChangedEventHandler OptionChanged;

    private FilterMode _filterSelectionChangeMode = FilterMode.Replace;
    private YesNoFilteringMode _isCriticalFilterMode = YesNoFilteringMode.Any;
    private LinePointTypeFilteringMode _nodeTypeFilteringMode = LinePointTypeFilteringMode.Any;
    private LinePointTypeFilteringMode _holdPartFilteringMode = LinePointTypeFilteringMode.Any;
    private string _tagFilter = string.Empty;
    private bool _isTagFilterRegex = false;
    private bool _onlyMatchLanesWithSelectedStartNodes = false;
    private bool _onlyMatchHoldsEndsWithSelectedStartNodes = false;
    private LeftRightFilteringMode _flickDirectionFilteringMode = LeftRightFilteringMode.Any;

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

    public FilterOptions(SelectionFilterViewModel selectionFilter)
    {
        SelectionFilter = selectionFilter;

        // Call setters on settings to update UI
        FilterSelectionChangeMode = _filterSelectionChangeMode;
        IsCriticalFilterMode = _isCriticalFilterMode;
        NodeTypeFilteringMode = _nodeTypeFilteringMode;
        HoldPartFilteringMode = _holdPartFilteringMode;
        TagFilter = _tagFilter;
        IsTagFilterRegex = _isTagFilterRegex;
        FlickDirectionFilteringMode = _flickDirectionFilteringMode;

        FilterBulletPalettesItems.AddRange(selectionFilter.Editor.Fumen.BulletPalleteList.Select(bpl => new FilterBulletPalettesItem(bpl)));
        FilterBulletPalettesItems.ForEach(i => i.PropertyChanged += (sender, args) =>
        {
            OptionChanged?.Invoke(sender, args);
        });

        PropertyChanged += (_, args) =>
        {
            OptionChanged?.Invoke(this, args);
        };

        FilterDockableLaneOptions.ForEach(x =>
        {
            x.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(FilterDockableLaneOption.IsSelected)) {
                    OptionChanged?.Invoke(this, new PropertyChangedEventArgs(""));
                }
            };
        });

        // Initialize bullet palletes list
        if (selectionFilter.Editor is not null) {
            foreach (var bullets in selectionFilter.Editor.Fumen.Bullets
                         .Concat<IBulletPalleteReferencable>(selectionFilter.Editor.Fumen.Bells)
                         .Where(b => b.ReferenceBulletPallete is not null)
                         .GroupBy(b => b.ReferenceBulletPallete)) {
                FilterBulletPalettesItems.First(b => b.BulletPalette == bullets.Key).IncludedBullets.AddRange(bullets);
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
            else if (tag == TagFilter) {
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
        } else if (OnlyMatchLanesWithSelectedStartNodes && obj is HoldEnd holdEnd) {
            if (!SelectionFilter.Editor.SelectObjects.Contains(holdEnd.ReferenceLaneStart)) {
                return false;
            }
        }

        return true;
    }
}

public sealed class FilterObjectTypeCategory : PropertyChangedBase
{
    private readonly SelectionFilterViewModel Filter;

    public ObservableCollection<FilterObjectTypesItem> Items { get; private set; } = new();

    private readonly string CategoryName;

    private string _categoryNameDisplay;
    public string CategoryNameDisplay
    {
        get => _categoryNameDisplay;
        private set => Set(ref _categoryNameDisplay, value);
    }

    public FilterObjectTypeCategory(SelectionFilterViewModel filter, string categoryName, IEnumerable<FilterObjectTypesItem> items)
    {
        Filter = filter;
        CategoryName = categoryName;

        Items.CollectionChanged += (_, args) =>
        {
            if (args.NewItems?.Count > 0) {
                foreach (var item in args.NewItems.Cast<FilterObjectTypesItem>()) {
                    item.PropertyChanged += (_, typeArgs) =>
                    {
                        if (typeArgs.PropertyName == nameof(FilterObjectTypesItem.IsSelected))
                            filter.FilterObjectTypeSelectedChanged(item);
                    };
                }
            }
        };

        Items.AddRange(items);
        UpdateCategoryNameDisplay();
    }

    public void UpdateCategoryNameDisplay()
    {
        var matches = Items.Sum(i => i.MatchingObjects.Count);
        CategoryNameDisplay = $"{CategoryName} ({matches})";
    }
}

public class FilterDockableLaneOption : PropertyChangedBase
{
    public string Name { get; init; }
    public Predicate<ILaneDockable> Filter { get; init; }

    private bool _isSelected = false;
    public bool IsSelected
    {
        get => _isSelected;
        set => Set(ref _isSelected, value);
    }
}

public class FilterObjectTypesItem : PropertyChangedBase
{
    private readonly SelectionFilterViewModel SelectionFilter;

    public List<ISelectableObject> MatchingObjects { get; private set; } = new();

    public FilterObjectTypesItem(SelectionFilterViewModel selectionFilter)
    {
        SelectionFilter = selectionFilter;
    }

    public string Text { get; init; }
    public Type[] Types { get; init; }

    public string Display => $"{Text} ({MatchingObjects.Count})";

    private bool _isSelected = false;
    public bool IsSelected
    {
        get => _isSelected;
        set => Set(ref _isSelected, value);
    }
}

public class FilterBulletPalettesItem : PropertyChangedBase
{
    public BulletPallete BulletPalette;
    public ObservableCollection<IBulletPalleteReferencable> IncludedBullets = new();

    public string Display => $"{BulletPalette.StrID} {(string.IsNullOrWhiteSpace(BulletPalette.EditorName) ? "" : BulletPalette.EditorName + " ")}({IncludedBullets.Count})";

    private bool _isSelected = false;

    public FilterBulletPalettesItem(BulletPallete bulletPalette)
    {
        BulletPalette = bulletPalette;
    }

    public bool IsSelected
    {
        get => _isSelected;
        set => Set(ref _isSelected, value);
    }
}

public enum LinePointTypeFilteringMode
{
    Head,
    Tail,
    Any
}

public enum YesNoFilteringMode
{
    Yes,
    No,
    Any
}

public enum LeftRightFilteringMode
{
    Left,
    Right,
    Any
}

public static class FilteringEnumExtensions
{
    public static Flick.FlickDirection ToFlickDirection(this LeftRightFilteringMode @this)
        => @this switch
        {
            LeftRightFilteringMode.Left => Flick.FlickDirection.Left,
            LeftRightFilteringMode.Right => Flick.FlickDirection.Right,
            _ => throw new ArgumentOutOfRangeException(nameof(@this), @this, null)
        };
}

public class ParamValueToBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value?.Equals(parameter);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value?.Equals(true) == true ? parameter : Binding.DoNothing;
    }
}