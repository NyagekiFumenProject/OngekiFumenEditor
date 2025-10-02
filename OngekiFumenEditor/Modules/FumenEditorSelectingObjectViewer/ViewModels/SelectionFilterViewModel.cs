#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using Caliburn.Micro;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.EditorObjects;
using OngekiFumenEditor.Base.EditorObjects.LaneCurve;
using OngekiFumenEditor.Base.EditorObjects.Svg;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Base.OngekiObjects.Beam;
using OngekiFumenEditor.Base.OngekiObjects.Lane;
using OngekiFumenEditor.Base.OngekiObjects.Wall;
using OngekiFumenEditor.Modules.FumenEditorSelectingObjectViewer.Base.SelectionFilter;
using OngekiFumenEditor.Modules.FumenObjectPropertyBrowser;
using OngekiFumenEditor.Modules.FumenVisualEditor.Kernel;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using OngekiFumenEditor.Properties;
using OngekiFumenEditor.Utils;
using InvalidOperationException = System.InvalidOperationException;

namespace OngekiFumenEditor.Modules.FumenEditorSelectingObjectViewer.ViewModels;

public class SelectionFilterViewModel : ViewAware
{
    public FumenEditorSelectingObjectViewerViewModel SelectionViewerTool { get; set; }
    public FumenVisualEditorViewModel? Editor => SelectionViewerTool.Editor;

    public ObservableCollection<OptionCategory> OptionCategories { get; } = new();
    public ObservableCollection<FilterObjectTypeCategory> FilterTypeCategories { get; set; } = new();

    public ObservableCollection<ISelectableObject> ObjectTypeFilterMatches { get; } = new();
    public ObservableCollection<ISelectableObject> OptionFilterRemovals { get; } = new();


    public bool IsInvertFilter
    {
        get;
        set
        {
            Set(ref field, value);
            UpdateFilterOutcomeText();
        }
    }

    public string FilterOutcomeText
    {
        get;
        private set => Set(ref field, value);
    }

    public SelectionFilterViewModel(FumenEditorSelectingObjectViewerViewModel selectionViewerTool)
    {
        SelectionViewerTool = selectionViewerTool;

        InitObjectTypeFilter();
        InitOptions();

        UpdateFilterOutcomeText();
    }

    public void OnSelectedItemsRefreshed()
    {
        FilterTypeCategories.SelectMany(c => c.Items).ForEach(i => i.MatchingObjects.Clear());
        ObjectTypeFilterMatches.Clear();

        // Add selected objects to each category
        if (Editor is not null) {
            foreach (var item in Editor.SelectObjects) {
                var matchingCategory = FilterTypeCategories.SelectMany(c => c.Items)
                    .FirstOrDefault(i => i.Types.Any(t => t.IsInstanceOfType(item)));
                matchingCategory?.MatchingObjects.Add(item);
            }

            // Refresh options
            foreach (var option in OptionCategories.SelectMany(c => c.Options)) {
                option.OnSelectionRefreshed(Editor.SelectObjects);
            }
        }

        // Change object type filters to match the currently selected objects
        foreach (var category in FilterTypeCategories) {
            foreach (var item in category.Items) {
                item.IsSelected = item.MatchingObjects.Count > 0;
            }

            category.UpdateCategoryNameDisplay();
        }

        OnOptionUpdated();
        UpdateFilterOutcomeText();
    }

    private void InitObjectTypeFilter()
    {
        if (!FilterTypeCategories.IsEmpty())
            throw new InvalidOperationException();

        FilterTypeCategories.AddRange([
            new(this, Resources.SelectionFilterObjectCategoryLane, [
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
            ]),

            new(this, Resources.SelectionFilterObjectCategoryDockable, [
                new(this) { Text = Resources.Tap, Types = [typeof(Tap)] },
                new(this) { Text = Resources.Hold, Types = [typeof(Hold), typeof(HoldEnd)] }
            ]),

            new(this, Resources.SelectionFilterObjectCategoryFloating, [
                new(this) { Text = Resources.Bell, Types = [typeof(Bell)] },
                new(this) { Text = Resources.Bullet, Types = [typeof(Bullet)] },
                new(this) { Text = Resources.Flick, Types = [typeof(Flick)] }
            ]),

            new(this, Resources.SelectionFilterObjectCategoryTimeline, [
                new(this) { Text = Resources.LaneBlock, Types = [typeof(LaneBlockArea), typeof(LaneBlockArea.LaneBlockAreaEndIndicator)] },
                new(this) { Text = Resources.ClickSE, Types = [typeof(ClickSE)] },
                new(this) { Text = Resources.InterpolatableSoflan, Types = [typeof(InterpolatableSoflan), typeof(InterpolatableSoflan.InterpolatableSoflanIndicator)] },
                new(this) { Text = Resources.KeyframeSoflan, Types = [typeof(KeyframeSoflan)] },
                new(this) { Text = Resources.DurationSoflan, Types = [typeof(IDurationSoflan)] },
                new(this) { Text = Resources.MeterChange, Types = [typeof(MeterChange)] },
                new(this) { Text = Resources.IndividualSoflanArea, Types = [typeof(IndividualSoflanArea)] },
            ]),

            new(this, Resources.SelectionFilterObjectCategoryMisc, [
                new(this) { Text = Resources.SvgPrefabFile, Types = [typeof(SvgImageFilePrefab)] },
                new(this) { Text = Resources.SvgPrefabText, Types = [typeof(SvgStringPrefab)] },
                new(this) { Text = Resources.Comment, Types = [typeof(Comment)] }
            ])
        ]);
    }

    private void InitOptions()
    {
        if (!OptionCategories.IsEmpty())
            throw new InvalidOperationException();

        OptionCategories.AddRange([
            new(Resources.SelectionFilter_OptionTabGeneral, [
                new TextWithRegexOption(Resources.SelectionFilter_OptionLabelTag, (obj, input, isRegex) =>
                {
                    if (obj is not OngekiObjectBase ongekiObj)
                        return true;
                    return ongekiObj.Tag == input;
                }),
            ]),
            new(Resources.SelectionFilter_OptionTabLanes, [
                new LaneNodeSpecificationOption(Resources.SelectionFilter_OptionLabelLaneNodeType)
            ]),
            new(Resources.SelectionFilter_OptionTabHitObjects, [
                BooleanOption.YesNoOption(Resources.SelectionFilter_OptionLabelIsCritical, (obj, yesNo) =>
                {
                    if (obj is not ICriticalableObject crit)
                        return true;
                    return crit.IsCritical == yesNo;
                }),
                BooleanOption.LeftRightOption(Resources.SelectionFilter_OptionLabelFlickDirection, (obj, leftRight) =>
                {
                    if (obj is not Flick flick)
                        return true;
                    return (leftRight && flick.Direction == Flick.FlickDirection.Left)
                           || (!leftRight && flick.Direction == Flick.FlickDirection.Right);
                }),
                new DockableObjectLaneFilterOption(Resources.SelectionFilter_OptionLabelDockLanes),
                new HoldObjectSpecificationOption(Resources.SelectionFilter_OptionLabelHoldType)
            ]),
            new(Resources.SelectionFilter_OptionTabBullets, [
                GenerateBulletPaletteOption()
            ]),
            new(Resources.SelectionFilter_OptionTabOther, [
                BooleanOption.LeftRightOption(Resources.SelectionFilter_OptionLabelLaneBlockDirection, (obj, leftRight) =>
                {
                    if (obj is not LaneBlockArea lbk)
                        return true;
                    return (lbk.Direction == LaneBlockArea.BlockDirection.Left) == leftRight;
                }),
            ])
        ]);

        foreach (var option in OptionCategories.SelectMany(o => o.Options)) {
            // Notify when an option is changed
            option.OptionValueChanged += OnOptionUpdated;
            option.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(SelectionFilterOption.IsEnabled)) {
                    OnOptionUpdated();
                }
            };
        }
    }

    private void OnOptionUpdated()
    {
        // Apply option filters
        UpdateOptionFilterRemovals();
        UpdateFilterOutcomeText();
    }

    private static SelectionFilterOption GenerateBulletPaletteOption()
    {
        var bulletPaletteOption = new BulletPaletteFilterOption(Resources.SelectionFilter_OptionLabelBulletPalette);

        FumenVisualEditorViewModel.LoadingFinishedEventHandler loaded = (sender, args) =>
        {
            bulletPaletteOption.UpdateOptions(args.Fumen.BulletPalleteList);
        };

        IoC.Get<IEditorDocumentManager>().OnActivateEditorChanged += (@new, old) =>
        {
            if (old is not null) {
                bulletPaletteOption.FumenUnloaded(old.Fumen);
                old.LoadingFinished -= loaded;
            }

            if (@new is not null) {
                @new.LoadingFinished += loaded;
                bulletPaletteOption.FumenLoaded(@new.Fumen);
            }
        };

        return bulletPaletteOption;
    }

    public IEnumerable<ISelectableObject> GetAllFilterMatches()
        => ObjectTypeFilterMatches.Except(OptionFilterRemovals);

    public void OnTypeFilterEnabledChanged(FilterObjectTypesItem filterType)
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
        if (IsInvertFilter) {
            FilterOutcomeText = Resources.SelectionFilter_ResultsLabelRemoveMode.Format(matches.Count());
        }
        else {
            FilterOutcomeText = Resources.SelectionFilter_ResultsLabelReplaceMode.Format(matches.Count());
        }
    }

    private IEnumerable<SelectionFilterOption> GetAllOptions()
        => OptionCategories.SelectMany(c => c.Options);

    private void UpdateOptionFilterRemovals()
    {
        OptionFilterRemovals.Clear();
        var enabledOptions = GetAllOptions().Where(o => o.IsEnabled).ToArray();
        OptionFilterRemovals.AddRange(ObjectTypeFilterMatches.Where(obj => enabledOptions.Any(opt => !opt.Filter((OngekiObjectBase)obj))));
    }

    public void ApplyFilterToSelection()
    {
        if (Editor is null)
            return;

        if (IsInvertFilter) {
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

        // Re-register items that remained in the selection
        foreach (var typeFilter in FilterTypeCategories.SelectMany(c => c.Items)) {
            ObjectTypeFilterMatches.AddRange(typeFilter.MatchingObjects);
        }
        UpdateFilterOutcomeText();
    }

    public void FilterObjectTypesSelectAll()
    {
        bool allSelected = true;
        foreach (var category in FilterTypeCategories) {
            foreach (var item in category.Items) {
                if (!item.IsSelected) {
                    allSelected = false;
                    item.IsSelected = true;
                }
            }
        }

        if (allSelected) {
            foreach (var category in FilterTypeCategories) {
                foreach (var item in category.Items) {
                    item.IsSelected = false;
                }
            }
        }
    }

    public void FilterObjectTypesReset()
    {
        foreach (var category in FilterTypeCategories) {
            foreach (var item in category.Items) {
                item.IsSelected = false;
            }
        }

        if (Editor is not null) {
            foreach (var category in FilterTypeCategories) {
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

    public string CategoryNameDisplay
    {
        get;
        private set => Set(ref field, value);
    }

    public string CategoryNameDisplayCheckCount
    {
        get;
        private set => Set(ref field, value);
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
                        if (typeArgs.PropertyName == nameof(FilterObjectTypesItem.IsSelected)) {
                            Filter.OnTypeFilterEnabledChanged(item);
                            UpdateCategoryNameDisplay();
                        }
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
        CategoryNameDisplayCheckCount = $"{CategoryName} ({Items.Count(i => i.IsSelected)} / {Items.Count})";
    }
}

public class FilterObjectTypesItem : PropertyChangedBase
{
    private readonly SelectionFilterViewModel SelectionFilter;

    public required string Text { get; init; }
    public required Type[] Types { get; init; }

    public ObservableCollection<ISelectableObject> MatchingObjects { get; } = new();

    public FilterObjectTypesItem(SelectionFilterViewModel selectionFilter)
    {
        SelectionFilter = selectionFilter;
        MatchingObjects.CollectionChanged += (_, _) => NotifyOfPropertyChange(nameof(Display));
    }

    public string Display => $"{Text} ({MatchingObjects.Count})";

    public bool IsSelected
    {
        get;
        set
        {
            Set(ref field, value);
            NotifyOfPropertyChange(nameof(Display));
        }
    } = false;
}

public class WideModeToVisibilityConverter : IValueConverter
{
    public bool IsInverse { get; init; }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (IsInverse) {
            if ((double)value < 700)
                return Visibility.Visible;
            else {
                return Visibility.Collapsed;
            }
        }
        else {
            if ((double)value < 700)
                return Visibility.Collapsed;
            else {
                return Visibility.Visible;
            }
        }
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}