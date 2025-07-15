using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Caliburn.Micro;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.EditorObjects;
using OngekiFumenEditor.Base.EditorObjects.LaneCurve;
using OngekiFumenEditor.Base.EditorObjects.Svg;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Base.OngekiObjects.Beam;
using OngekiFumenEditor.Base.OngekiObjects.Lane;
using OngekiFumenEditor.Base.OngekiObjects.Wall;
using OngekiFumenEditor.Modules.FumenObjectPropertyBrowser;
using OngekiFumenEditor.Modules.FumenVisualEditor.Kernel;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using OngekiFumenEditor.Properties;
using OngekiFumenEditor.Utils;

namespace OngekiFumenEditor.Modules.SelectionFilter.ViewModels;

public class SelectionFilterViewModel : ViewAware
{
    public FumenVisualEditorViewModel Editor { get; }

    public SelectionFilterOptionsViewModel FilterOptions { get; }

    public ObservableCollection<ISelectableObject> ObjectTypeFilterMatches { get; } = new();
    public ObservableCollection<ISelectableObject> OptionFilterRemovals { get; } = new();

    private string _filterOutcomeText;
    public string FilterOutcomeText
    {
        get => _filterOutcomeText;
        private set => Set(ref _filterOutcomeText, value);
    }

    // Filter categories

    public ObservableCollection<FilterObjectTypeCategory> FilterCategories { get; set; } = new();

    public SelectionFilterViewModel()
    {
        Editor = IoC.Get<IEditorDocumentManager>().CurrentActivatedEditor;
        FilterOptions = new(this);

        InitObjectTypeFilter();

        FilterOptions.OptionChanged += () =>
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
        FilterCategories.Add(new(this, Resources.SelectionFilterObjectCategoryLane, [
            new(this) { Text = Resources.WallLeft, Types = [typeof(WallLeftNext), typeof(WallLeftStart)] },
            new(this) { Text = Resources.LaneLeft, Types = [typeof(LaneLeftNext), typeof(LaneLeftStart)] },
            new(this) { Text = Resources.LaneCenter, Types = [typeof(LaneCenterNext), typeof(LaneCenterStart)] },
            new(this) { Text = Resources.LaneRight, Types = [typeof(LaneRightNext), typeof(LaneRightStart)] },
            new(this) { Text = Resources.WallRight, Types = [typeof(WallRightNext), typeof(WallRightStart)] },
            new(this) { Text = Resources.LaneColorful, Types = [typeof(ColorfulLaneNext), typeof(ColorfulLaneStart)] },
            new(this) { Text = Resources.EnemyLane, Types = [typeof(EnemyLaneNext), typeof(EnemyLaneStart)] },
            new(this) { Text = Resources.AutoPlayFaderLane, Types = [typeof(AutoplayFaderLaneNext), typeof(AutoplayFaderLaneStart)] },
            new(this) { Text = Resources.Beam, Types = [typeof(BeamNext), typeof(BeamStart)] },
            new(this) { Text = Resources.CurveControlPoint, Types = [typeof(LaneCurvePathControlObject)] }
        ]));

        FilterCategories.Add(new(this, Resources.SelectionFilterObjectCategoryDockable, [
            new(this) { Text = Resources.Tap, Types = [typeof(Tap)] },
            new(this) { Text = Resources.Hold, Types = [typeof(Hold), typeof(HoldEnd)] }
        ]));

        FilterCategories.Add(new(this, Resources.SelectionFilterObjectCategoryFloating, [
            new(this) { Text = Resources.Bell, Types = [typeof(Bell)] },
            new(this) { Text = Resources.Bullet, Types = [typeof(Bullet)] },
            new(this) { Text = Resources.Flick, Types = [typeof(Flick)] }
        ]));

        FilterCategories.Add(new(this, Resources.SelectionFilterObjectCategoryTimeline, [
            new(this) { Text = Resources.LaneBlock, Types = [typeof(LaneBlockArea), typeof(LaneBlockArea.LaneBlockAreaEndIndicator)] },
            new(this) { Text = Resources.ClickSE, Types = [typeof(ClickSE)] },
            new(this) { Text = Resources.InterpolatableSoflan, Types = [typeof(InterpolatableSoflan), typeof(InterpolatableSoflan.InterpolatableSoflanIndicator)] },
            new(this) { Text = Resources.KeyframeSoflan, Types = [typeof(KeyframeSoflan)] },
            new(this) { Text = Resources.DurationSoflan, Types = [typeof(IDurationSoflan)] },
            new(this) { Text = Resources.MeterChange, Types = [typeof(MeterChange)] },
            new(this) { Text = Resources.IndividualSoflanArea, Types = [typeof(IndividualSoflanArea)] },
        ]));

        FilterCategories.Add(new(this, Resources.SelectionFilterObjectCategoryMisc, [
            new(this) { Text = Resources.SvgPrefabFile, Types = [typeof(SvgImageFilePrefab)] },
            new(this) { Text = Resources.SvgPrefabText, Types = [typeof(SvgStringPrefab)] },
            new(this) { Text = Resources.Comment, Types = [typeof(Comment)] }
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

    public IEnumerable<ISelectableObject> GetAllFilterMatches()
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
        if (FilterOptions.IsInvertFilterEnabled) {
            FilterOutcomeText = Resources.SelectionFilter_ResultsLabelRemoveMode.Format(matches.Count());
        }
        else {
            FilterOutcomeText = Resources.SelectionFilter_ResultsLabelReplaceMode.Format(matches.Count());
        }
    }

    public void ApplyFilterToSelection()
    {
        if (FilterOptions.IsInvertFilterEnabled) {
            foreach (var selectableObject in GetAllFilterMatches()) {
                selectableObject.IsSelected = false;
            }
        }
        else {
            foreach (var selectedObject in Editor.SelectObjects.Except(GetAllFilterMatches())) {
                selectedObject.IsSelected = false;
            }
        }

        IoC.Get<IFumenObjectPropertyBrowser>().RefreshSelected(Editor);
    }

    public void FilterObjectTypesSelectAll()
    {
        bool allSelected = true;
        foreach (var category in FilterCategories) {
            foreach (var item in category.Items) {
                if (!item.IsSelected) {
                    allSelected = false;
                    item.IsSelected = true;
                }
            }
        }

        if (allSelected) {
            foreach (var category in FilterCategories) {
                foreach (var item in category.Items) {
                    item.IsSelected = false;
                }
            }
        }
    }

    public void FilterObjectTypesReset()
    {
        foreach (var category in FilterCategories) {
            foreach (var item in category.Items) {
                item.IsSelected = false;
            }
        }

        if (Editor is not null) {
            foreach (var category in FilterCategories) {
                foreach (var item in category.Items) {
                    if (item.MatchingObjects.Count > 0) {
                        item.IsSelected = true;
                    }
                }
            }
        }
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

    public List<ISelectableObject> MatchingObjects { get; } = new();

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
    public BulletPallete BulletPalette { get; }

    public List<ISelectableObject> MatchingBullets { get; private set; } = new();
    public List<ISelectableObject> MatchingBells { get; private set; } = new();

    public string Display =>
        (BulletPalette is null
            ? Resources.NoBulletPalette
            : $"{BulletPalette.StrID}{(string.IsNullOrWhiteSpace(BulletPalette.EditorName) ? "" : $" {BulletPalette.EditorName}")}")
        + $" ({MatchingBullets.Count} | {MatchingBells.Count})";

    public int ItemsCount => MatchingBullets.Count + MatchingBells.Count;

    public FilterBulletPalettesItem(BulletPallete bulletPalette)
    {
        BulletPalette = bulletPalette;
    }

    private bool _isSelected = false;
    public bool IsSelected
    {
        get => _isSelected;
        set => Set(ref _isSelected, value);
    }
}