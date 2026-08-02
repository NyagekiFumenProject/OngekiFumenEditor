#nullable enable
using Gekimini.Avalonia.ViewModels;
using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OngekiFumenEditor.Avalonia.Base;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects.Projectiles;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects.Projectiles.Enums;
using OngekiFumenEditor.Avalonia.Modules.FumenEditorSelectingObjectViewer.Base.SelectionFilter;
using OngekiFumenEditor.Avalonia.Assets.Languages;
using OngekiFumenEditor.Avalonia.Utils;

namespace OngekiFumenEditor.Avalonia.Modules.FumenEditorSelectingObjectViewer.ViewModels;

public partial class SelectionFilterViewModel : ViewModelBase
{
    public FumenEditorSelectingObjectViewerViewModel SelectionViewerTool { get; }
    public OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.ViewModels.FumenVisualEditorViewModel? Editor => SelectionViewerTool.Editor;

    public ObservableCollection<OptionCategory> OptionCategories { get; } = [];
    public ObservableCollection<FilterObjectTypeCategory> FilterTypeCategories { get; } = [];
    public ObservableCollection<ISelectableObject> OptionFilterRemovals { get; } = [];

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

        FilterTypeCategories.Add(new FilterObjectTypeCategory(this, Lang.SelectionFilterObjectCategoryDockable, new[]
        {
            new FilterObjectTypesItem { Text = Lang.Tap, Types = [typeof(Tap)] },
            new FilterObjectTypesItem { Text = Lang.Hold, Types = [typeof(Hold), typeof(HoldEnd)] }
        }));

        FilterTypeCategories.Add(new FilterObjectTypeCategory(this, Lang.SelectionFilterObjectCategoryFloating, new[]
        {
            new FilterObjectTypesItem { Text = Lang.Bell, Types = [typeof(Bell)] },
            new FilterObjectTypesItem { Text = Lang.Bullet, Types = [typeof(Bullet)] },
            new FilterObjectTypesItem { Text = Lang.Flick, Types = [typeof(Flick)] }
        }));

        FilterTypeCategories.Add(new FilterObjectTypeCategory(this, Lang.SelectionFilterObjectCategoryTimeline, new[]
        {
            new FilterObjectTypesItem { Text = Lang.ClickSE, Types = [typeof(ClickSE)] },
            new FilterObjectTypesItem { Text = Lang.MeterChange, Types = [typeof(MeterChange)] }
        }));
    }

    private void InitOptions()
    {
        if (OptionCategories.Count > 0)
            return;

        OptionCategories.Add(new OptionCategory(Lang.SelectionFilter_OptionTabGeneral, new SelectionFilterOption[]
        {
            new TextWithRegexOption(Lang.SelectionFilter_OptionLabelTag, (obj, input, isRegex) =>
            {
                if (obj is not OngekiObjectBase ongekiObj)
                    return FilterOptionResult.NotApplicable;
                if (isRegex)
                    return Regex.IsMatch(ongekiObj.Tag, input) ? FilterOptionResult.Match : FilterOptionResult.NoMatch;
                return ongekiObj.Tag == input ? FilterOptionResult.Match : FilterOptionResult.NoMatch;
            })
        }));

        OptionCategories.Add(new OptionCategory(Lang.SelectionFilter_OptionTabHitObjects, new SelectionFilterOption[]
        {
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
                    ? FilterOptionResult.Match : FilterOptionResult.NoMatch;
            })
        }));

        OptionCategories.Add(new OptionCategory(Lang.SelectionFilter_OptionTabBullets, new SelectionFilterOption[]
        {
            new BooleanOption(Lang.BulletSize, (obj, smallLarge) =>
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
            new EnumSpecificationOption<BulletType>(Lang.BulletType, (obj, value) =>
            {
                if (obj is not Bullet bullet)
                    return FilterOptionResult.NotApplicable;
                return bullet.TypeValue == value ? FilterOptionResult.Match : FilterOptionResult.NoMatch;
            })
        }));

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

