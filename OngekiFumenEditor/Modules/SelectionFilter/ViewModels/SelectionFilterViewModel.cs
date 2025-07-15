using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Windows.Data;
using AngleSharp.Common;
using Caliburn.Micro;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.EditorObjects;
using OngekiFumenEditor.Base.EditorObjects.LaneCurve;
using OngekiFumenEditor.Base.EditorObjects.Svg;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Base.OngekiObjects.Beam;
using OngekiFumenEditor.Base.OngekiObjects.Lane;
using OngekiFumenEditor.Base.OngekiObjects.Wall;
using OngekiFumenEditor.Modules.FumenVisualEditor.Kernel;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using OngekiFumenEditor.Utils;
using Binding = System.Windows.Data.Binding;

namespace OngekiFumenEditor.Modules.SelectionFilter.ViewModels;

public class SelectionFilterViewModel : ViewAware
{
    public FumenVisualEditorViewModel Editor { get; }

    public SelectionFilterOptionsViewModel FilterOptions { get; }

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
            ? "No palette"
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