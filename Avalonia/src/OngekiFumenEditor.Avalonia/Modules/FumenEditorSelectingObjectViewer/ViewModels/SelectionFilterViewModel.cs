#nullable enable
using Gekimini.Avalonia.ViewModels;
using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OngekiFumenEditor.Avalonia.Base;
using OngekiFumenEditor.Avalonia.Base.EditorObjects;
using OngekiFumenEditor.Avalonia.Base.EditorObjects.LaneCurve;
using OngekiFumenEditor.Avalonia.Base.EditorObjects.Svg;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects.Beam;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects.Lane;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects.Projectiles;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects.Projectiles.Enums;
using OngekiFumenEditor.Avalonia.Modules.FumenEditorSelectingObjectViewer.Base.SelectionFilter;
using OngekiFumenEditor.Avalonia.Assets.Languages;
using OngekiFumenEditor.Avalonia.Modules.FumenObjectPropertyBrowser;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.ViewModels;
using OngekiFumenEditor.Avalonia.Utils;

namespace OngekiFumenEditor.Avalonia.Modules.FumenEditorSelectingObjectViewer.ViewModels;

public partial class SelectionFilterViewModel : ViewModelBase
{
    public FumenEditorSelectingObjectViewerViewModel SelectionViewerTool { get; }
    public OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.ViewModels.FumenVisualEditorViewModel? Editor => SelectionViewerTool.Editor;

    public ObservableCollection<OptionCategory> OptionCategories { get; } = [];
    public ObservableCollection<FilterObjectTypeCategory> FilterTypeCategories { get; } = [];
    public ObservableCollection<ISelectableObject> OptionFilterRemovals { get; } = [];
    private BulletPaletteFilterOption bulletPaletteOption = null!;

    public bool IsInvertFilter
    {
        get => field;
        set
        {
            if (SetProperty(ref field, value))
                UpdateFilterOutcomeText();
        }
    }

    public string FilterOutcomeText
    {
        get => field;
        private set => SetProperty(ref field, value);
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
        foreach (var item in FilterTypeCategories.SelectMany(c => c.Items))
            item.MatchingObjects.Clear();

        foreach (var option in OptionCategories.SelectMany(c => c.Options))
            option.ResetOptionMatchCount();

        if (Editor is not null)
        {
            foreach (var item in Editor.SelectObjects)
            {
                var matchingItem = FilterTypeCategories.SelectMany(c => c.Items)
                    .FirstOrDefault(i => i.Types.Any(t => t.IsInstanceOfType(item)));

                matchingItem?.MatchingObjects.Add(item);

                foreach (var option in OptionCategories.SelectMany(c => c.Options))
                    option.IncrementOptionMatchCount((OngekiObjectBase)item);
            }
        }

        foreach (var category in FilterTypeCategories)
        {
            foreach (var item in category.Items)
                item.IsSelected = item.MatchingObjects.Count > 0;
        }

        UpdateOptionFilterRemovals();
        UpdateFilterOutcomeText();
    }

    public void OnTypeFilterEnabledChanged(FilterObjectTypesItem _)
    {
        UpdateFilterOutcomeText();
    }

    internal void OnEditorChanged(FumenVisualEditorViewModel? editor)
    {
        bulletPaletteOption.SetFumen(editor?.Fumen);
    }

    internal void OnEditorFumenChanged(FumenVisualEditorViewModel? editor)
    {
        if (ReferenceEquals(Editor, editor))
            bulletPaletteOption.SetFumen(editor?.Fumen);
    }

    private void OnOptionUpdated()
    {
        UpdateOptionFilterRemovals();
        UpdateFilterOutcomeText();
    }

    private void UpdateFilterOutcomeText()
    {
        var matches = GetAllFilterMatches();
        FilterOutcomeText = IsInvertFilter
            ? Lang.SelectionFilter_ResultsLabelRemoveMode.Format(matches.Count())
            : Lang.SelectionFilter_ResultsLabelReplaceMode.Format(matches.Count());
    }

    private IEnumerable<SelectionFilterOption> GetAllOptions() => OptionCategories.SelectMany(c => c.Options);

    private void UpdateOptionFilterRemovals()
    {
        OptionFilterRemovals.Clear();
        var enabledOptions = GetAllOptions().Where(o => o.IsEnabled).ToArray();
        foreach (var obj in GetAllMatchingTypeObjects().Where(obj => enabledOptions.Any(opt => opt.Filter((OngekiObjectBase)obj) == FilterOptionResult.NoMatch)))
            OptionFilterRemovals.Add(obj);
    }

    private IEnumerable<ISelectableObject> GetAllMatchingTypeObjects()
        => FilterTypeCategories.SelectMany(c => c.Items).Where(i => i.IsSelected).SelectMany(i => i.MatchingObjects);

    private IEnumerable<ISelectableObject> GetAllFilterMatches()
        => GetAllMatchingTypeObjects().Except(OptionFilterRemovals);

    private void InitObjectTypeFilter()
    {
        if (FilterTypeCategories.Count > 0)
            return;

        FilterTypeCategories.AddRange([
            new(this, Lang.SelectionFilterObjectCategoryLane, [
                new() { Text = Lang.WallLeft, Types = [typeof(WallLeftNext), typeof(WallLeftStart)] },
                new() { Text = Lang.LaneLeft, Types = [typeof(LaneLeftNext), typeof(LaneLeftStart)] },
                new() { Text = Lang.LaneCenter, Types = [typeof(LaneCenterNext), typeof(LaneCenterStart)] },
                new() { Text = Lang.LaneRight, Types = [typeof(LaneRightNext), typeof(LaneRightStart)] },
                new() { Text = Lang.WallRight, Types = [typeof(WallRightNext), typeof(WallRightStart)] },
                new() { Text = Lang.LaneColorful, Types = [typeof(ColorfulLaneNext), typeof(ColorfulLaneStart)] },
                new() { Text = Lang.EnemyLane, Types = [typeof(EnemyLaneNext), typeof(EnemyLaneStart)] },
                new() { Text = Lang.AutoPlayFaderLane, Types = [typeof(AutoplayFaderLaneNext), typeof(AutoplayFaderLaneStart)] },
                new() { Text = Lang.Beam, Types = [typeof(BeamNext), typeof(BeamStart)] },
                new() { Text = Lang.CurveControlPoint, Types = [typeof(LaneCurvePathControlObject)] }
            ]),
            new(this, Lang.SelectionFilterObjectCategoryDockable, [
                new() { Text = Lang.Tap, Types = [typeof(Tap)] },
                new() { Text = Lang.Hold, Types = [typeof(Hold), typeof(HoldEnd)] }
            ]),
            new(this, Lang.SelectionFilterObjectCategoryFloating, [
                new() { Text = Lang.Bell, Types = [typeof(Bell)] },
                new() { Text = Lang.Bullet, Types = [typeof(Bullet)] },
                new() { Text = Lang.Flick, Types = [typeof(Flick)] }
            ]),
            new(this, Lang.SelectionFilterObjectCategoryTimeline, [
                new() { Text = Lang.LaneBlock, Types = [typeof(LaneBlockArea), typeof(LaneBlockArea.LaneBlockAreaEndIndicator)] },
                new() { Text = Lang.ClickSE, Types = [typeof(ClickSE)] },
                new() { Text = Lang.InterpolatableSoflan, Types = [typeof(InterpolatableSoflan), typeof(InterpolatableSoflan.InterpolatableSoflanIndicator)] },
                new() { Text = Lang.KeyframeSoflan, Types = [typeof(KeyframeSoflan)] },
                new() { Text = Lang.DurationSoflan, Types = [typeof(IDurationSoflan)] },
                new() { Text = Lang.MeterChange, Types = [typeof(MeterChange)] },
                new() { Text = Lang.IndividualSoflanArea, Types = [typeof(IndividualSoflanArea)] }
            ]),
            new(this, Lang.SelectionFilterObjectCategoryMisc, [
                new() { Text = Lang.SvgPrefabFile, Types = [typeof(SvgImageFilePrefab)] },
                new() { Text = Lang.SvgPrefabText, Types = [typeof(SvgStringPrefab)] },
                new() { Text = Lang.Comment, Types = [typeof(Comment)] }
            ])
        ]);
    }

    private void InitOptions()
    {
        if (OptionCategories.Count > 0)
            return;

        bulletPaletteOption = new BulletPaletteFilterOption(Lang.SelectionFilter_OptionLabelBulletPalette);

        OptionCategories.AddRange([
            new(Lang.SelectionFilter_OptionTabGeneral, [
                new TextWithRegexOption(Lang.SelectionFilter_OptionLabelTag, (obj, input, isRegex) =>
                {
                    if (obj is not OngekiObjectBase ongekiObj)
                        return FilterOptionResult.NotApplicable;
                    if (isRegex)
                        return Regex.IsMatch(ongekiObj.Tag, input) ? FilterOptionResult.Match : FilterOptionResult.NoMatch;
                    return ongekiObj.Tag == input ? FilterOptionResult.Match : FilterOptionResult.NoMatch;
                })
            ]),
            new(Lang.SelectionFilter_OptionTabLanes, [
                new LaneNodeSpecificationOption(Lang.SelectionFilter_OptionLabelLaneNodeType),
                new EnumSpecificationOption<SelectionStatusSpecification>(Lang.SelectionFilter_OptionLabelCurveNextSelected,
                    (obj, input) =>
                    {
                        if (obj is not LaneCurvePathControlObject curveObj)
                            return FilterOptionResult.NotApplicable;
                        return curveObj.RefCurveObject.IsSelected == input.ToBool()
                            ? FilterOptionResult.Match
                            : FilterOptionResult.NoMatch;
                    }),
                new EnumSpecificationOption<SelectionStatusSpecification>(Lang.SelectionFilter_OptionLabelCurvePrevSelected,
                    (obj, input) =>
                    {
                        if (obj is not LaneCurvePathControlObject curveObj)
                            return FilterOptionResult.NotApplicable;
                        return curveObj.RefCurveObject.PrevObject?.IsSelected == input.ToBool()
                            ? FilterOptionResult.Match
                            : FilterOptionResult.NoMatch;
                    })
            ]),
            new(Lang.SelectionFilter_OptionTabHitObjects, [
                BooleanOption.YesNoOption(Lang.SelectionFilter_OptionLabelIsCritical, (obj, yesNo) =>
                {
                    if (obj is not ICriticalableObject crit)
                        return FilterOptionResult.NotApplicable;
                    return crit.IsCritical == yesNo ? FilterOptionResult.Match : FilterOptionResult.NoMatch;
                }),
                BooleanOption.LeftRightOption(Lang.SelectionFilter_OptionLabelFlickDirection, (obj, leftRight) =>
                {
                    if (obj is not Flick flick)
                        return FilterOptionResult.NotApplicable;
                    return (leftRight && flick.Direction == Flick.FlickDirection.Left)
                           || (!leftRight && flick.Direction == Flick.FlickDirection.Right)
                        ? FilterOptionResult.Match
                        : FilterOptionResult.NoMatch;
                }),
                new DockableObjectLaneFilterOption(Lang.SelectionFilter_OptionLabelDockLanes),
                new HeadTailSpecificationOption<Hold, HoldEnd>(Lang.SelectionFilter_OptionLabelHoldType,
                    holdEnd => holdEnd.RefHold, holdStart => holdStart.HoldEnd)
            ]),
            new(Lang.SelectionFilter_OptionTabBullets, [
                bulletPaletteOption,
                new BooleanOption(Lang.BulletSize, (obj, smallLarge) =>
                {
                    if (obj is not Bullet bullet)
                        return FilterOptionResult.NotApplicable;
                    return (bullet.SizeValue == BulletSize.Normal) == smallLarge
                        ? FilterOptionResult.Match
                        : FilterOptionResult.NoMatch;
                })
                {
                    TrueText = BulletSize.Normal.ToString(),
                    FalseText = BulletSize.Large.ToString()
                },
                new EnumSpecificationOption<BulletType>(Lang.BulletType, (obj, value) =>
                {
                    if (obj is not Bullet bullet)
                        return FilterOptionResult.NotApplicable;
                    return bullet.TypeValue == value ? FilterOptionResult.Match : FilterOptionResult.NoMatch;
                })
            ]),
            new(Lang.SelectionFilter_OptionTabOther, [
                BooleanOption.LeftRightOption(Lang.SelectionFilter_OptionLabelLaneBlockDirection, (obj, leftRight) =>
                {
                    if (obj is not LaneBlockArea laneBlock)
                        return FilterOptionResult.NotApplicable;
                    return (laneBlock.Direction == LaneBlockArea.BlockDirection.Left) == leftRight
                        ? FilterOptionResult.Match
                        : FilterOptionResult.NoMatch;
                }),
                new HeadTailSpecificationOption<LaneBlockArea, LaneBlockArea.LaneBlockAreaEndIndicator>(
                    Lang.SelectionFilter_OptionLabelLaneBlockType,
                    end => end.RefLaneBlockArea,
                    start => start.EndIndicator),
                new HeadTailSpecificationOption<Soflan, Soflan.SoflanEndIndicator>(
                    Lang.SelectionFilter_OptionLabelSoflanAreaType,
                    end => end.RefSoflan,
                    start => start.EndIndicator)
            ])
        ]);

        foreach (var option in OptionCategories.SelectMany(o => o.Options))
        {
            option.OptionValueChanged += OnOptionUpdated;
            option.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(SelectionFilterOption.IsEnabled))
                    OnOptionUpdated();
            };
        }
    }

    [RelayCommand]
    public void ApplyFilterToSelection()
    {
        if (Editor is null)
            return;

        if (IsInvertFilter)
        {
            foreach (var selectableObject in GetAllFilterMatches())
                selectableObject.IsSelected = false;
        }
        else
        {
            foreach (var selectedObject in Editor.SelectObjects.Except(GetAllFilterMatches()))
                selectedObject.IsSelected = false;
        }

        IoC.Get<IFumenObjectPropertyBrowser>().RefreshSelected(Editor);

        UpdateOptionFilterRemovals();
        UpdateFilterOutcomeText();
    }

    [RelayCommand]
    public void SelectAllObjectTypes()
    {
        var allSelected = true;
        foreach (var category in FilterTypeCategories)
        {
            foreach (var item in category.Items)
            {
                if (!item.IsSelected)
                {
                    allSelected = false;
                    item.IsSelected = true;
                }
            }
        }

        if (!allSelected)
            return;

        foreach (var category in FilterTypeCategories)
        {
            foreach (var item in category.Items)
                item.IsSelected = false;
        }
    }

    [RelayCommand]
    public void ResetSelectedObjectTypes()
    {
        foreach (var category in FilterTypeCategories)
        {
            foreach (var item in category.Items)
                item.IsSelected = false;
        }

        if (Editor is null)
            return;

        foreach (var category in FilterTypeCategories)
        {
            foreach (var item in category.Items.Where(item => item.MatchingObjects.Count > 0))
                item.IsSelected = true;
        }
    }

    [RelayCommand]
    public void ResetFilterOptions()
    {
        foreach (var opt in OptionCategories.SelectMany(c => c.Options))
            opt.IsEnabled = false;
    }
}

