#nullable enable
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using CommunityToolkit.Mvvm.ComponentModel;
using OngekiFumenEditor.Avalonia.Base;
using OngekiFumenEditor.Avalonia.Base.Collections;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects.Projectiles;
using OngekiFumenEditor.Avalonia.Assets.Languages;

namespace OngekiFumenEditor.Avalonia.Modules.FumenEditorSelectingObjectViewer.Base.SelectionFilter;

public class OptionCategory : ObservableObject
{
    public ObservableCollection<SelectionFilterOption> Options { get; } = [];

    public string Name { get; }
    public string DisplayName => $"{Name} ({Options.Count(o => o.IsEnabled)} / {Options.Count})";

    public OptionCategory(string name, IEnumerable<SelectionFilterOption> options)
    {
        Options.CollectionChanged += (_, args) =>
        {
            foreach (var item in args.NewItems?.Cast<SelectionFilterOption>() ?? Array.Empty<SelectionFilterOption>())
                item.PropertyChanged += OnItemPropertyChanged;

            foreach (var item in args.OldItems?.Cast<SelectionFilterOption>() ?? Array.Empty<SelectionFilterOption>())
                item.PropertyChanged -= OnItemPropertyChanged;
        };

        foreach (var item in options)
            Options.Add(item);

        Name = name;
    }

    private void OnItemPropertyChanged(object? _, PropertyChangedEventArgs propArgs)
    {
        if (propArgs.PropertyName == nameof(SelectionFilterOption.IsEnabled))
            OnPropertyChanged(new PropertyChangedEventArgs(nameof(DisplayName)));
    }
}

public abstract class SelectionFilterOption : ObservableObject
{
    public delegate void OptionValueChangedEventHandler();
    public event OptionValueChangedEventHandler? OptionValueChanged;

    public bool IsEnabled
    {
        get => field;
        set => SetProperty(ref field, value);
    }

    public string Text { get; }

    protected SelectionFilterOption(string text)
    {
        Text = text;
    }

    public abstract FilterOptionResult Filter(OngekiObjectBase obj);
    public abstract void IncrementOptionMatchCount(OngekiObjectBase obj);
    public abstract void ResetOptionMatchCount();

    protected void NotifyOptionValueChanged()
    {
        OptionValueChanged?.Invoke();
    }
}

public class TextWithRegexOption : SelectionFilterOption
{
    public delegate FilterOptionResult FilterPredicate(OngekiObjectBase obj, string input, bool regexIsEnabled);

    public bool IsRegex
    {
        get => field;
        set
        {
            if (SetProperty(ref field, value))
                NotifyOptionValueChanged();
        }
    }

    public string InputText
    {
        get => field;
        set
        {
            if (SetProperty(ref field, value))
                NotifyOptionValueChanged();
        }
    } = string.Empty;

    public int MatchCount
    {
        get => field;
        private set => SetProperty(ref field, value);
    }

    public FilterPredicate Predicate { get; }

    public TextWithRegexOption(string text, FilterPredicate filter) : base(text)
    {
        Predicate = filter;
    }

    public override FilterOptionResult Filter(OngekiObjectBase obj)
    {
        return Predicate(obj, InputText, IsRegex);
    }

    public override void IncrementOptionMatchCount(OngekiObjectBase obj)
    {
        if (Filter(obj) == FilterOptionResult.Match)
            MatchCount++;
    }

    public override void ResetOptionMatchCount()
    {
        MatchCount = 0;
    }
}

public abstract class SelectionFilterOption<T> : SelectionFilterOption where T : struct
{
    protected readonly Func<OngekiObjectBase, T, FilterOptionResult> Predicate;

    protected SelectionFilterOption(string text, Func<OngekiObjectBase, T, FilterOptionResult> predicate)
        : base(text)
    {
        Predicate = predicate;
    }

    public T Value
    {
        get => field;
        set
        {
            if (SetProperty(ref field, value))
                NotifyOptionValueChanged();
        }
    }

    public sealed override FilterOptionResult Filter(OngekiObjectBase obj)
    {
        return Predicate(obj, Value);
    }
}

public class BooleanOption : SelectionFilterOption<bool>
{
    public int FalseMatches
    {
        get => field;
        private set => SetProperty(ref field, value);
    }

    public int TrueMatches
    {
        get => field;
        private set => SetProperty(ref field, value);
    }

    public string FalseText
    {
        get => field;
        init => SetProperty(ref field, value);
    } = string.Empty;

    public string TrueText
    {
        get => field;
        init => SetProperty(ref field, value);
    } = string.Empty;

    public BooleanOption(string text, Func<OngekiObjectBase, bool, FilterOptionResult> filter)
        : base(text, filter)
    {
        Value = true;
    }

    public override void IncrementOptionMatchCount(OngekiObjectBase obj)
    {
        if (Predicate(obj, true) == FilterOptionResult.Match)
            TrueMatches++;
        if (Predicate(obj, false) == FilterOptionResult.Match)
            FalseMatches++;
    }

    public override void ResetOptionMatchCount()
    {
        TrueMatches = 0;
        FalseMatches = 0;
    }

    public static BooleanOption YesNoOption(string text, Func<OngekiObjectBase, bool, FilterOptionResult> filter)
    {
        return new(text, filter)
        {
            TrueText = Lang.SelectionFilter_ChoiceYes,
            FalseText = Lang.SelectionFilter_ChoiceNo
        };
    }

    public static BooleanOption LeftRightOption(string text, Func<OngekiObjectBase, bool, FilterOptionResult> filter)
    {
        return new(text, filter)
        {
            TrueText = Lang.DirectionLeft,
            FalseText = Lang.DirectionRight
        };
    }
}

public abstract class EnumSpecificationOption : SelectionFilterOption
{
    public IReadOnlyList<EnumSelectionItem> Selections { get; }
    public abstract int SelectedOptionMatchCount { get; set; }
    public abstract object Value { get; set; }

    /// <summary>
    /// Exposes <see cref="Value"/> as a strongly typed selection row so that an Avalonia ComboBox
    /// can use a compiled item template and bind SelectedItem directly.
    /// </summary>
    public EnumSelectionItem? SelectedItem
    {
        get => Selections.FirstOrDefault(item => Equals(item.Value, Value));
        set
        {
            if (value is not null)
                Value = value.Value;
        }
    }

    protected EnumSpecificationOption(string text, Type enumType, Dictionary<object, string>? selectionsText) : base(text)
    {
        Selections = (selectionsText ?? Enum.GetValues(enumType).Cast<object>().ToDictionary(x => x, x => x.ToString()!))
            .Select(pair => new EnumSelectionItem(pair.Key, pair.Value))
            .ToArray();
    }
}

public class EnumSpecificationOption<T> : EnumSpecificationOption where T : Enum
{
    public override object Value
    {
        get => TypedValue;
        set
        {
            if (value is not T tValue)
                throw new InvalidOperationException();
            TypedValue = tValue;
            NotifyOptionValueChanged();
        }
    }

    public T TypedValue
    {
        get => field;
        set
        {
            if (SetProperty(ref field, value))
            {
                OnPropertyChanged(new PropertyChangedEventArgs(nameof(Value)));
                OnPropertyChanged(new PropertyChangedEventArgs(nameof(SelectedItem)));
                NotifyOptionValueChanged();
            }
        }
    } = default!;

    public Dictionary<T, int> OptionMatchCounts { get; } = Enum.GetValues(typeof(T)).Cast<T>().ToDictionary(x => x, _ => 0);

    public delegate FilterOptionResult FilterPredicate(OngekiObjectBase obj, T input);

    public FilterPredicate Predicate { get; }

    public override int SelectedOptionMatchCount
    {
        get => field;
        set => SetProperty(ref field, value);
    }

    public EnumSpecificationOption(string text, FilterPredicate predicate, Dictionary<T, string>? selectionsText = null)
        : base(text, typeof(T), selectionsText?.ToDictionary(kv => (object)kv.Key, kv => kv.Value))
    {
        Predicate = predicate;
        OptionValueChanged += () => { SelectedOptionMatchCount = OptionMatchCounts[TypedValue]; };
    }

    public override void IncrementOptionMatchCount(OngekiObjectBase obj)
    {
        foreach (var v in Enum.GetValues(typeof(T)).Cast<T>())
        {
            var res = Predicate(obj, v);
            if (res == FilterOptionResult.NotApplicable)
                break;
            if (res == FilterOptionResult.Match)
                OptionMatchCounts[v]++;
        }

        SelectedOptionMatchCount = OptionMatchCounts.ContainsKey(TypedValue) ? OptionMatchCounts[TypedValue] : 0;
    }

    public override void ResetOptionMatchCount()
    {
        foreach (var key in OptionMatchCounts.Keys.ToArray())
            OptionMatchCounts[key] = 0;

        SelectedOptionMatchCount = 0;
    }

    public override FilterOptionResult Filter(OngekiObjectBase obj)
    {
        return Predicate(obj, TypedValue);
    }
}

public sealed record EnumSelectionItem(object Value, string Text);

public class HeadTailSpecificationOption<THead, TTail> : EnumSpecificationOption<HeadTailSpecification>
    where THead : OngekiObjectBase, ISelectableObject
    where TTail : OngekiObjectBase, ISelectableObject
{
    public delegate THead HeadGetter(TTail obj);
    public delegate TTail TailGetter(THead obj);

    public HeadTailSpecificationOption(string text, HeadGetter headGetter, TailGetter tailGetter,
        Dictionary<HeadTailSpecification, string>? selectionsText = null)
        : base(text, GetPredicate(headGetter, tailGetter),
            selectionsText ?? FilterEnumExtensions.HeadTailSpecificationMapStartEnd.ToDictionary())
    {
    }

    private static FilterPredicate GetPredicate(HeadGetter headGetter, TailGetter tailGetter)
    {
        return (obj, input) =>
        {
            switch (obj)
            {
                case THead head:
                {
                    var tailObj = tailGetter(head);
                    return input switch
                    {
                        HeadTailSpecification.Head => FilterOptionResult.Match,
                        HeadTailSpecification.HeadNoChild when tailObj is null || !tailObj.IsSelected => FilterOptionResult.Match,
                        HeadTailSpecification.HeadWithChild when tailObj.IsSelected => FilterOptionResult.Match,
                        _ => FilterOptionResult.NoMatch
                    };
                }
                case TTail tail:
                {
                    var headObj = headGetter(tail);
                    return input switch
                    {
                        HeadTailSpecification.Tail => FilterOptionResult.Match,
                        HeadTailSpecification.TailNoParent when !headObj.IsSelected => FilterOptionResult.Match,
                        HeadTailSpecification.TailWithParent when headObj.IsSelected => FilterOptionResult.Match,
                        _ => FilterOptionResult.NoMatch
                    };
                }
                default:
                    return FilterOptionResult.NotApplicable;
            }
        };
    }
}

public sealed class LaneNodeSpecificationOption(string text)
    : EnumSpecificationOption<HeadTailSpecification>(text, LaneNodePredicate, SelectionsTextMap)
{
    private static readonly Dictionary<HeadTailSpecification, string> SelectionsTextMap = new()
    {
        [HeadTailSpecification.Head] = Lang.SelectionFilter_HeadTailLaneNode_Head,
        [HeadTailSpecification.Tail] = Lang.SelectionFilter_HeadTailLaneNode_Tail,
        [HeadTailSpecification.HeadWithChild] = Lang.SelectionFilter_HeadTailLaneNode_HeadWithChild,
        [HeadTailSpecification.HeadNoChild] = Lang.SelectionFilter_HeadTailLaneNode_HeadNoChild,
        [HeadTailSpecification.TailWithParent] = Lang.SelectionFilter_HeadTailLaneNode_TailWithParent,
        [HeadTailSpecification.TailNoParent] = Lang.SelectionFilter_HeadTailLaneNode_TailNoParent
    };

    private static FilterOptionResult LaneNodePredicate(OngekiObjectBase obj, HeadTailSpecification input)
    {
        switch (obj)
        {
            case ConnectableStartObject startObj:
                return input switch
                {
                    HeadTailSpecification.Head => FilterOptionResult.Match,
                    HeadTailSpecification.HeadNoChild when !startObj.Children.Any(c => c.IsSelected) => FilterOptionResult.Match,
                    HeadTailSpecification.HeadWithChild when startObj.Children.Any(c => c.IsSelected) => FilterOptionResult.Match,
                    _ => FilterOptionResult.NoMatch
                };
            case ConnectableChildObjectBase childObj:
                return input switch
                {
                    HeadTailSpecification.Tail => FilterOptionResult.Match,
                    HeadTailSpecification.TailNoParent when !childObj.ReferenceStartObject.IsSelected => FilterOptionResult.Match,
                    HeadTailSpecification.TailWithParent when childObj.ReferenceStartObject.IsSelected => FilterOptionResult.Match,
                    _ => FilterOptionResult.NoMatch
                };
            default:
                return FilterOptionResult.NotApplicable;
        }
    }
}

public sealed class BulletPaletteFilterOption : SelectionFilterOption
{
    public ObservableCollection<BulletPaletteFilterItem> Items { get; } = [];
    private Dictionary<BulletPallete, BulletPaletteFilterItem> paletteTable = new();
    private BulletPaletteFilterItem nullPaletteItem;
    private OngekiFumen? currentFumen;

    public int FilterMatches
    {
        get => field;
        set => SetProperty(ref field, value);
    }

    public BulletPaletteFilterOption(string text) : base(text)
    {
        OptionValueChanged += UpdateFilterMatches;

        PropertyChangedEventHandler handler = (_, propChange) =>
        {
            if (propChange.PropertyName == nameof(BulletPaletteFilterItem.IsSelected))
                NotifyOptionValueChanged();

            if (propChange.PropertyName is nameof(BulletPaletteFilterItem.IsSelected) or nameof(BulletPaletteFilterItem.Text))
            {
                OnPropertyChanged(nameof(IsAllSelected));
                OnPropertyChanged(nameof(SelectionSummary));
            }
        };

        Items.CollectionChanged += (_, e) =>
        {
            foreach (var i in e.NewItems?.Cast<BulletPaletteFilterItem>() ?? [])
                i.PropertyChanged += handler;
            foreach (var i in e.OldItems?.Cast<BulletPaletteFilterItem>() ?? [])
                i.PropertyChanged -= handler;

            OnPropertyChanged(nameof(IsAllSelected));
            OnPropertyChanged(nameof(SelectionSummary));
        };

        nullPaletteItem = new BulletPaletteFilterItem(null);
    }

    public void FumenLoaded(OngekiFumen fumen)
    {
        SetFumen(fumen);
    }

    public void FumenUnloaded(OngekiFumen fumen)
    {
        if (ReferenceEquals(currentFumen, fumen))
            SetFumen(null);
    }

    internal void SetFumen(OngekiFumen? fumen)
    {
        if (ReferenceEquals(currentFumen, fumen))
            return;

        if (currentFumen is not null)
            currentFumen.BulletPalleteList.CollectionChanged -= BulletPaletteCollectionChanged;

        currentFumen = fumen;
        if (currentFumen is not null)
        {
            currentFumen.BulletPalleteList.CollectionChanged += BulletPaletteCollectionChanged;
            UpdateOptions(currentFumen.BulletPalleteList);
        }
        else
        {
            UpdateOptionsCore([]);
        }
    }

    public void UpdateOptions(BulletPalleteList paletteList)
    {
        UpdateOptionsCore(paletteList);
    }

    private void UpdateOptionsCore(IEnumerable<BulletPallete> palettes)
    {
        Items.Clear();

        nullPaletteItem = new BulletPaletteFilterItem(null);
        Items.Add(nullPaletteItem);
        Items.Add(new BulletPaletteFilterItem(BulletPallete.DummyCustomPallete));
        foreach (var p in palettes)
            Items.Add(new BulletPaletteFilterItem(p));

        paletteTable = Items.Where(i => i != nullPaletteItem && i.Palette is not null).ToDictionary(i => i.Palette!, i => i);
    }

    private void BulletPaletteCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        UpdateOptions((BulletPalleteList)sender!);
    }

    public override FilterOptionResult Filter(OngekiObjectBase obj)
    {
        if (obj is not IBulletPalleteReferencable bullet)
            return FilterOptionResult.NotApplicable;

        var selectedPalettes = Items.Where(p => p.IsSelected).ToArray();
        if (selectedPalettes.Length == 0)
            return bullet.ReferenceBulletPallete == null ? FilterOptionResult.Match : FilterOptionResult.NoMatch;

        return selectedPalettes.Any(i => i.Palette == bullet.ReferenceBulletPallete)
            ? FilterOptionResult.Match
            : FilterOptionResult.NoMatch;
    }

    public override void IncrementOptionMatchCount(OngekiObjectBase obj)
    {
        if (obj is not IBulletPalleteReferencable bullet)
            return;

        var item = bullet.ReferenceBulletPallete == null ? nullPaletteItem : paletteTable[bullet.ReferenceBulletPallete];
        if (bullet is Bullet)
            item.BulletCount++;
        else if (bullet is Bell)
            item.BellCount++;

        UpdateFilterMatches();
    }

    public override void ResetOptionMatchCount()
    {
        foreach (var palette in Items)
        {
            palette.BulletCount = 0;
            palette.BellCount = 0;
        }

        FilterMatches = 0;
    }

    private void UpdateFilterMatches()
    {
        FilterMatches = Items.Where(i => i.IsSelected).Sum(i => i.BulletCount + i.BellCount);
    }

    public bool IsAllSelected
    {
        get => Items.Count > 0 && Items.All(item => item.IsSelected);
        set
        {
            foreach (var item in Items)
                item.IsSelected = value;

            OnPropertyChanged();
        }
    }

    public string SelectionSummary => string.Join(", ", Items.Where(item => item.IsSelected).Select(item => item.Text)) is { Length: > 0 } summary
        ? summary
        : "...";
}

public sealed class BulletPaletteFilterItem(BulletPallete? palette) : ObservableObject
{
    public BulletPallete? Palette { get; } = palette;

    public bool IsSelected
    {
        get => field;
        set => SetProperty(ref field, value);
    }

    public int BulletCount
    {
        get => field;
        set
        {
            if (SetProperty(ref field, value))
                OnPropertyChanged(new PropertyChangedEventArgs(nameof(Text)));
        }
    }

    public int BellCount
    {
        get => field;
        set
        {
            if (SetProperty(ref field, value))
                OnPropertyChanged(new PropertyChangedEventArgs(nameof(Text)));
        }
    }

    public string Text
    {
        get
        {
            var baseText = Palette is null
                ? Lang.NoBulletPalette
                : Palette == BulletPallete.DummyCustomPallete
                    ? Palette.EditorName
                    : $"{Palette.StrID} {Palette.EditorName}";
            return $"{baseText} ({BulletCount} | {BellCount})";
        }
    }
}

public sealed class DockableObjectLaneFilterOption : SelectionFilterOption
{
    public ObservableCollection<DockableObjectLaneFilterItem> Values { get; } = [];

    public int FilterMatches
    {
        get => field;
        set => SetProperty(ref field, value);
    }

    public DockableObjectLaneFilterOption(string text) : base(text)
    {
        foreach (var d in Enum.GetValues<DockableTargetSpecification>())
            Values.Add(new DockableObjectLaneFilterItem(d));

        foreach (var v in Values)
        {
            v.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(DockableObjectLaneFilterItem.IsSelected))
                {
                    NotifyOptionValueChanged();
                    UpdateFilterMatches();
                    OnPropertyChanged(nameof(IsAllSelected));
                    OnPropertyChanged(nameof(SelectionSummary));
                }
                else if (args.PropertyName == nameof(DockableObjectLaneFilterItem.Text))
                    OnPropertyChanged(nameof(SelectionSummary));
            };
        }
    }

    public override FilterOptionResult Filter(OngekiObjectBase obj)
    {
        if (obj is not ILaneDockable dockable)
            return FilterOptionResult.NotApplicable;

        return GetItemFromObject(dockable) is { IsSelected: true } ? FilterOptionResult.Match : FilterOptionResult.NoMatch;
    }

    public override void IncrementOptionMatchCount(OngekiObjectBase obj)
    {
        if (obj is not ILaneDockable dockable)
            return;

        var item = GetItemFromObject(dockable);
        if (item is not null)
            item.MatchCount++;

        UpdateFilterMatches();
    }

    private void UpdateFilterMatches()
    {
        FilterMatches = Values.Where(i => i.IsSelected).Sum(i => i.MatchCount);
    }

    private DockableObjectLaneFilterItem? GetItemFromObject(ILaneDockable dockable)
    {
        return Values.SingleOrDefault(i =>
            i.DockLane == (dockable.ReferenceLaneStart?.LaneType ?? LaneType.Undefined).GetDockableTargetSpecification());
    }

    public override void ResetOptionMatchCount()
    {
        foreach (var item in Values)
            item.MatchCount = 0;
        FilterMatches = 0;
    }

    public bool IsAllSelected
    {
        get => Values.Count > 0 && Values.All(item => item.IsSelected);
        set
        {
            foreach (var item in Values)
                item.IsSelected = value;

            OnPropertyChanged();
        }
    }

    public string SelectionSummary => string.Join(", ", Values.Where(item => item.IsSelected).Select(item => item.Text)) is { Length: > 0 } summary
        ? summary
        : "...";
}

public sealed class DockableObjectLaneFilterItem(DockableTargetSpecification dockLane) : ObservableObject
{
    public DockableTargetSpecification DockLane { get; } = dockLane;

    public int MatchCount
    {
        get => field;
        set
        {
            if (SetProperty(ref field, value))
                OnPropertyChanged(new PropertyChangedEventArgs(nameof(Text)));
        }
    }

    public bool IsSelected
    {
        get => field;
        set => SetProperty(ref field, value);
    }

    public string Text => $"{DockLane.ToResourceName()} ({MatchCount})";
}

public enum HeadTailSpecification
{
    Head = 0,
    Tail = 1,
    HeadNoChild = 2,
    HeadWithChild = 3,
    TailNoParent = 4,
    TailWithParent = 5
}

public enum DockableTargetSpecification
{
    NoLane,
    WallLeft,
    LaneLeft,
    LaneCenter,
    LaneRight,
    WallRight
}

public enum SelectionStatusSpecification
{
    Selected,
    Unselected
}

public enum FilterOptionResult
{
    Match,
    NoMatch,
    NotApplicable
}

public static class FilterEnumExtensions
{
    public static readonly ImmutableDictionary<HeadTailSpecification, string> HeadTailSpecificationMapStartEnd =
        new Dictionary<HeadTailSpecification, string>
        {
            [HeadTailSpecification.Head] = Lang.SelectionFilter_HeadTailHoldObject_Head,
            [HeadTailSpecification.Tail] = Lang.SelectionFilter_HeadTailHoldObject_Tail,
            [HeadTailSpecification.HeadWithChild] = Lang.SelectionFilter_HeadTailHoldObject_HeadWithChild,
            [HeadTailSpecification.HeadNoChild] = Lang.SelectionFilter_HeadTailHoldObject_HeadNoChild,
            [HeadTailSpecification.TailWithParent] = Lang.SelectionFilter_HeadTailHoldObject_TailWithParent,
            [HeadTailSpecification.TailNoParent] = Lang.SelectionFilter_HeadTailHoldObject_TailNoParent
        }.ToImmutableDictionary();

    public static LaneType GetLaneType(this DockableTargetSpecification spec)
    {
        return spec switch
        {
            DockableTargetSpecification.NoLane => LaneType.Undefined,
            DockableTargetSpecification.WallLeft => LaneType.WallLeft,
            DockableTargetSpecification.LaneLeft => LaneType.Left,
            DockableTargetSpecification.LaneCenter => LaneType.Center,
            DockableTargetSpecification.LaneRight => LaneType.Right,
            DockableTargetSpecification.WallRight => LaneType.WallRight,
            _ => throw new ArgumentOutOfRangeException(nameof(spec), spec, null)
        };
    }

    public static DockableTargetSpecification GetDockableTargetSpecification(this LaneType laneType)
    {
        return laneType switch
        {
            LaneType.Undefined => DockableTargetSpecification.NoLane,
            LaneType.WallLeft => DockableTargetSpecification.WallLeft,
            LaneType.Left => DockableTargetSpecification.LaneLeft,
            LaneType.Center => DockableTargetSpecification.LaneCenter,
            LaneType.Right => DockableTargetSpecification.LaneRight,
            LaneType.WallRight => DockableTargetSpecification.WallRight,
            _ => throw new ArgumentOutOfRangeException(nameof(laneType), laneType, null)
        };
    }

    public static string ToResourceName(this DockableTargetSpecification spec)
    {
        return spec switch
        {
            DockableTargetSpecification.NoLane => Lang.SelectionFilter_None,
            DockableTargetSpecification.WallLeft => Lang.WallLeft,
            DockableTargetSpecification.WallRight => Lang.WallRight,
            DockableTargetSpecification.LaneLeft => Lang.LaneLeft,
            DockableTargetSpecification.LaneCenter => Lang.LaneCenter,
            DockableTargetSpecification.LaneRight => Lang.LaneRight,
            _ => throw new ArgumentOutOfRangeException(nameof(spec), spec, null)
        };
    }

    public static bool ToBool(this SelectionStatusSpecification spec)
    {
        return spec switch
        {
            SelectionStatusSpecification.Selected => true,
            SelectionStatusSpecification.Unselected => false,
            _ => throw new ArgumentOutOfRangeException(nameof(spec), spec, null)
        };
    }
}

