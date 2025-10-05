#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
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
using OngekiFumenEditor.Base.OngekiObjects.Projectiles.Enums;
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
    public FumenEditorSelectingObjectViewerViewModel SelectionViewerTool { get; }
    public FumenVisualEditorViewModel? Editor => SelectionViewerTool.Editor;

    public ObservableCollection<OptionCategory> OptionCategories { get; } = new();
    public ObservableCollection<FilterObjectTypeCategory> FilterTypeCategories { get; } = new();

    public ObservableCollection<ISelectableObject> OptionFilterRemovals { get; } = new();

    public bool IsInvertFilter
    {
        get;
        set
        {
            Set(ref field, value);
            UpdateFilterOutcomeText();
        }
    } = false;

    public string FilterOutcomeText
    {
        get;
        private set => Set(ref field, value);
    } = string.Empty;

    public SelectionFilterViewModel(FumenEditorSelectingObjectViewerViewModel selectionViewerTool)
    {
        SelectionViewerTool = selectionViewerTool;

        InitObjectTypeFilter();
        InitOptions();

        UpdateFilterOutcomeText();
    }

    public void OnSelectedItemsRefreshed()
    {
        // Clear type matchers
        // TODO Use CollectionChanged events so we don't have to recreate the list every time we change selection.
        //  This requires changes to other Editor modules.
        FilterTypeCategories.SelectMany(c => c.Items).ForEach(i => i.MatchingObjects.Clear());

        OptionCategories.SelectMany(c => c.Options).ForEach(o => o.ResetOptionMatchCount());

        // Add selected objects to each category
        if (Editor is not null) {
            foreach (var item in Editor.SelectObjects) {
                if (FilterTypeCategories.SelectMany(c => c.Items)
                        .FirstOrDefault(i => i.Types.Any(t => t.IsInstanceOfType(item))) is { } matchingItem) {
                    matchingItem.MatchingObjects.Add(item);
                }

                OptionCategories.SelectMany(c => c.Options).ForEach(o => o.IncrementOptionMatchCount((OngekiObjectBase)item));
            }
        }

        // Change object type filters to match the currently selected objects
        foreach (var category in FilterTypeCategories) {
            foreach (var item in category.Items) {
                item.IsSelected = item.MatchingObjects.Count > 0;
            }
        }

        UpdateOptionFilterRemovals();
        UpdateFilterOutcomeText();
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

        FumenVisualEditorViewModel.LoadingFinishedEventHandler loaded = (_, args) =>
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

    public void OnTypeFilterEnabledChanged(FilterObjectTypesItem _)
    {
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
        OptionFilterRemovals.AddRange(GetAllMatchingTypeObjects().Where(obj => enabledOptions.Any(opt => opt.Filter((OngekiObjectBase)obj) == FilterOptionResult.NoMatch)));
    }

    private IEnumerable<ISelectableObject> GetAllMatchingTypeObjects()
        => FilterTypeCategories.SelectMany(c => c.Items).Where(i => i.IsSelected).SelectMany(i => i.MatchingObjects);

    private IEnumerable<ISelectableObject> GetAllFilterMatches()
        => GetAllMatchingTypeObjects().Except(OptionFilterRemovals);

    #region Option Generation
    private void InitObjectTypeFilter()
    {
        if (!FilterTypeCategories.IsEmpty())
            throw new InvalidOperationException();

        FilterTypeCategories.AddRange([
            new(this, Resources.SelectionFilterObjectCategoryLane, [
                new() { Text = Resources.WallLeft, Types = [typeof(WallLeftNext), typeof(WallLeftStart)] },
                new() { Text = Resources.LaneLeft, Types = [typeof(LaneLeftNext), typeof(LaneLeftStart)] },
                new() { Text = Resources.LaneCenter, Types = [typeof(LaneCenterNext), typeof(LaneCenterStart)] },
                new() { Text = Resources.LaneRight, Types = [typeof(LaneRightNext), typeof(LaneRightStart)] },
                new() { Text = Resources.WallRight, Types = [typeof(WallRightNext), typeof(WallRightStart)] },
                new() { Text = Resources.LaneColorful, Types = [typeof(ColorfulLaneNext), typeof(ColorfulLaneStart)] },
                new() { Text = Resources.EnemyLane, Types = [typeof(EnemyLaneNext), typeof(EnemyLaneStart)] },
                new() { Text = Resources.AutoPlayFaderLane, Types = [typeof(AutoplayFaderLaneNext), typeof(AutoplayFaderLaneStart)] },
                new() { Text = Resources.Beam, Types = [typeof(BeamNext), typeof(BeamStart)] },
                new() { Text = Resources.CurveControlPoint, Types = [typeof(LaneCurvePathControlObject)] }
            ]),

            new(this, Resources.SelectionFilterObjectCategoryDockable, [
                new() { Text = Resources.Tap, Types = [typeof(Tap)] },
                new() { Text = Resources.Hold, Types = [typeof(Hold), typeof(HoldEnd)] }
            ]),

            new(this, Resources.SelectionFilterObjectCategoryFloating, [
                new() { Text = Resources.Bell, Types = [typeof(Bell)] },
                new() { Text = Resources.Bullet, Types = [typeof(Bullet)] },
                new() { Text = Resources.Flick, Types = [typeof(Flick)] }
            ]),

            new(this, Resources.SelectionFilterObjectCategoryTimeline, [
                new() { Text = Resources.LaneBlock, Types = [typeof(LaneBlockArea), typeof(LaneBlockArea.LaneBlockAreaEndIndicator)] },
                new() { Text = Resources.ClickSE, Types = [typeof(ClickSE)] },
                new() { Text = Resources.InterpolatableSoflan, Types = [typeof(InterpolatableSoflan), typeof(InterpolatableSoflan.InterpolatableSoflanIndicator)] },
                new() { Text = Resources.KeyframeSoflan, Types = [typeof(KeyframeSoflan)] },
                new() { Text = Resources.DurationSoflan, Types = [typeof(IDurationSoflan)] },
                new() { Text = Resources.MeterChange, Types = [typeof(MeterChange)] },
                new() { Text = Resources.IndividualSoflanArea, Types = [typeof(IndividualSoflanArea)] },
            ]),

            new(this, Resources.SelectionFilterObjectCategoryMisc, [
                new() { Text = Resources.SvgPrefabFile, Types = [typeof(SvgImageFilePrefab)] },
                new() { Text = Resources.SvgPrefabText, Types = [typeof(SvgStringPrefab)] },
                new() { Text = Resources.Comment, Types = [typeof(Comment)] }
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
                        return FilterOptionResult.NotApplicable;
                    if (isRegex)
                        return Regex.IsMatch(ongekiObj.Tag, input) ? FilterOptionResult.Match : FilterOptionResult.NoMatch;
                    return ongekiObj.Tag == input ? FilterOptionResult.Match : FilterOptionResult.NoMatch;
                }),
            ]),
            new(Resources.SelectionFilter_OptionTabLanes, [
                new LaneNodeSpecificationOption(Resources.SelectionFilter_OptionLabelLaneNodeType),
                new EnumSpecificationOption<SelectionStatusSpecification>(Resources.SelectionFilter_OptionLabelCurveNextSelected,
                    (obj, input) =>
                    {
                        if (obj is not LaneCurvePathControlObject curveObj)
                            return FilterOptionResult.NotApplicable;
                        return curveObj.RefCurveObject.IsSelected == input.ToBool() ? FilterOptionResult.Match : FilterOptionResult.NoMatch;
                    }),
                new EnumSpecificationOption<SelectionStatusSpecification>(Resources.SelectionFilter_OptionLabelCurvePrevSelected,
                    (obj, input) =>
                    {
                        if (obj is not LaneCurvePathControlObject curveObj)
                            return FilterOptionResult.NotApplicable;
                        return curveObj.RefCurveObject.PrevObject?.IsSelected == input.ToBool() ? FilterOptionResult.Match : FilterOptionResult.NoMatch;
                    }),
            ]),
            new(Resources.SelectionFilter_OptionTabHitObjects, [
                BooleanOption.YesNoOption(Resources.SelectionFilter_OptionLabelIsCritical, (obj, yesNo) =>
                {
                    if (obj is not ICriticalableObject crit)
                        return FilterOptionResult.NotApplicable;
                    return crit.IsCritical == yesNo ? FilterOptionResult.Match : FilterOptionResult.NoMatch;
                }),
                BooleanOption.LeftRightOption(Resources.SelectionFilter_OptionLabelFlickDirection, (obj, leftRight) =>
                {
                    if (obj is not Flick flick)
                        return FilterOptionResult.NotApplicable;
                    return (leftRight && flick.Direction == Flick.FlickDirection.Left)
                           || (!leftRight && flick.Direction == Flick.FlickDirection.Right)
                        ? FilterOptionResult.Match : FilterOptionResult.NoMatch;
                }),
                new DockableObjectLaneFilterOption(Resources.SelectionFilter_OptionLabelDockLanes),
                new HeadTailSpecificationOption<Hold, HoldEnd>(Resources.SelectionFilter_OptionLabelHoldType,
                    holdEnd => holdEnd.RefHold, holdStart => holdStart.HoldEnd),
            ]),
            new(Resources.SelectionFilter_OptionTabBullets, [
                GenerateBulletPaletteOption(),
                new BooleanOption(Resources.BulletSize, (obj, smallLarge) =>
                {
                    if (obj is not Bullet bullet)
                        return FilterOptionResult.NotApplicable;
                    return (bullet.SizeValue == BulletSize.Normal) == smallLarge
                        ? FilterOptionResult.Match : FilterOptionResult.NoMatch;
                })
                {
                    TrueText = BulletSize.Normal.ToString(),
                    FalseText = BulletSize.Large.ToString()
                },
                new EnumSpecificationOption<BulletType>(Resources.BulletType, (obj, value) =>
                {
                    if (obj is not Bullet bullet)
                        return FilterOptionResult.NotApplicable;
                    return bullet.TypeValue == value ? FilterOptionResult.Match : FilterOptionResult.NoMatch;
                })
            ]),
            new(Resources.SelectionFilter_OptionTabOther, [
                BooleanOption.LeftRightOption(Resources.SelectionFilter_OptionLabelLaneBlockDirection, (obj, leftRight) =>
                {
                    if (obj is not LaneBlockArea lbk)
                        return FilterOptionResult.NotApplicable;
                    return (lbk.Direction == LaneBlockArea.BlockDirection.Left) == leftRight
                        ? FilterOptionResult.Match : FilterOptionResult.NoMatch;
                }),
                new HeadTailSpecificationOption<LaneBlockArea, LaneBlockArea.LaneBlockAreaEndIndicator>(Resources.SelectionFilter_OptionLabelLaneBlockType,
                    obj => obj.RefLaneBlockArea, obj => obj.EndIndicator),
                new HeadTailSpecificationOption<Soflan, Soflan.SoflanEndIndicator>(Resources.SelectionFilter_OptionLabelSoflanAreaType,
                    soflanEnd => soflanEnd.RefSoflan, soflanStart => soflanStart.EndIndicator)
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
    #endregion

    #region Actions

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

        UpdateOptionFilterRemovals();
        UpdateFilterOutcomeText();
    }

    public void SelectAllObjectTypes()
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

    public void ResetSelectedObjectTypes()
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

    public void ResetFilterOptions()
    {
        foreach (var opt in OptionCategories.SelectMany(c => c.Options)) {
            opt.IsEnabled = false;
        }
    }

    #endregion
}

public class WideModeToVisibilityConverter : IValueConverter
{
    public bool IsInverse { get; init; }

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is null)
            return null;

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

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}