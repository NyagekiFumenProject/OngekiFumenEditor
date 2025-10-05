#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using Caliburn.Micro;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.Collections;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Properties;
using OngekiFumenEditor.Utils;

namespace OngekiFumenEditor.Modules.FumenEditorSelectingObjectViewer.Base.SelectionFilter;

public class OptionCategory : PropertyChangedBase
{
    public ObservableCollection<SelectionFilterOption> Options { get; } = new();

    public string Name { get; }
    public string DisplayName => $"{Name} ({Options.Count(o => o.IsEnabled)} / {Options.Count})";

    public OptionCategory(string name, IEnumerable<SelectionFilterOption> options)
    {
        Options.CollectionChanged += (_, args) =>
        {
            foreach (var item in args.NewItems?.Cast<SelectionFilterOption>() ?? Array.Empty<SelectionFilterOption>()) {
                item.PropertyChanged += OnItemPropertyChanged;
            }
            foreach (var item in args.OldItems?.Cast<SelectionFilterOption>() ?? Array.Empty<SelectionFilterOption>()) {
                item.PropertyChanged -= OnItemPropertyChanged;
            }
        };

        Options.AddRange(options);
        Name = name;
    }

    private void OnItemPropertyChanged(object? _, PropertyChangedEventArgs propArgs)
    {
        if (propArgs.PropertyName == nameof(SelectionFilterOption.IsEnabled)) {
            NotifyOfPropertyChange(nameof(DisplayName));
        }
    }
}

/// <summary>
/// Base class for any filter option.
/// </summary>
public abstract class SelectionFilterOption : PropertyChangedBase
{
    public delegate void OptionValueChangedEventHandler();
    public event OptionValueChangedEventHandler? OptionValueChanged;

    public bool IsEnabled
    {
        get;
        set => Set(ref field, value);
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

/// <summary>
/// A filter option that requires user text input, with a regex option.
/// </summary>
public class TextWithRegexOption : SelectionFilterOption
{
    public delegate FilterOptionResult FilterPredicate(OngekiObjectBase obj, string input, bool regexIsEnabled);

    public bool IsRegex
    {
        get;
        set
        {
            Set(ref field, value);
            NotifyOptionValueChanged();
        }
    } = false;

    public string InputText
    {
        get;
        set
        {
            Set(ref field, value);
            NotifyOptionValueChanged();
        }
    } = string.Empty;

    public int MatchCount
    {
        get;
        private set => Set(ref field, value);
    } = 0;

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

/// <summary>
/// Base class for single-value filter options of value types (ex. bool, int).
/// </summary>
public abstract class SelectionFilterOption<T> : SelectionFilterOption
    where T : struct
{
    protected readonly Func<OngekiObjectBase, T, FilterOptionResult> Predicate;

    protected SelectionFilterOption(string text, Func<OngekiObjectBase, T, FilterOptionResult> predicate)
        : base(text)
    {
        Predicate = predicate;
    }

    public T Value
    {
        get;
        set
        {
            Set(ref field, value);
            NotifyOptionValueChanged();
        }
    } = default;

    public sealed override FilterOptionResult Filter(OngekiObjectBase obj)
    {
        return Predicate(obj, Value);
    }
}

/// <summary>
/// A filter option that allows for selecting between two values.
/// </summary>
public class BooleanOption : SelectionFilterOption<bool>
{
    public int FalseMatches
    {
        get;
        private set => Set(ref field, value);
    }

    public int TrueMatches
    {
        get;
        private set => Set(ref field, value);
    }

    public string FalseText
    {
        get;
        init => Set(ref field, value);
    } = string.Empty;

    public string TrueText
    {
        get;
        init => Set(ref field, value);
    } = string.Empty;

    public BooleanOption(string text, Func<OngekiObjectBase, bool, FilterOptionResult> filter)
        : base(text, filter)
    {
        Value = true;
    }

    public override void IncrementOptionMatchCount(OngekiObjectBase obj)
    {
        if (Predicate(obj, true) == FilterOptionResult.Match) {
            TrueMatches++;
        }

        if (Predicate(obj, false) == FilterOptionResult.Match) {
            FalseMatches++;
        }
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
            TrueText = Resources.SelectionFilter_ChoiceYes,
            FalseText = Resources.SelectionFilter_ChoiceNo
        };
    }

    public static BooleanOption LeftRightOption(string text, Func<OngekiObjectBase, bool, FilterOptionResult> filter)
    {
        return new(text, filter)
        {
            TrueText = Resources.DirectionLeft,
            FalseText = Resources.DirectionRight
        };
    }
}

/// <summary>
/// A filter option that allows for selecting a single value of an enum type.
/// </summary>
public abstract class EnumSpecificationOption : SelectionFilterOption
{
    public Dictionary<object, string> SelectionsText { get; }
    public abstract int SelectedOptionMatchCount { get; set; }
    public abstract object Value { get; set; }

    protected EnumSpecificationOption(string text, Type enumType, Dictionary<object, string>? selectionsText) : base(text)
    {
        if (selectionsText is null)
            SelectionsText = Enum.GetValues(enumType).Cast<object>().ToDictionary(x => x, x => x.ToString()!);
        else
            SelectionsText = selectionsText;
    }
}

/// <inheritdoc />
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
        get;
        set
        {
            Set(ref field, value);
            NotifyOfPropertyChange(nameof(Value));
            NotifyOptionValueChanged();
        }
    }

    public Dictionary<T, int> OptionMatchCounts { get; } = Enum.GetValues(typeof(T)).Cast<T>().ToDictionary(x => x, _ => 0);

    public delegate FilterOptionResult FilterPredicate(OngekiObjectBase obj, T input);

    public FilterPredicate Predicate { get; }

    public override int SelectedOptionMatchCount
    {
        get;
        set => Set(ref field, value);
    } = 0;

    public EnumSpecificationOption(string text, FilterPredicate predicate, Dictionary<T, string>? selectionsText = null)
        : base(text, typeof(T), selectionsText?.ToDictionary(kv => (object)kv.Key, kv => kv.Value))
    {
        TypedValue = default!;
        Predicate = predicate;

        OptionValueChanged += () =>
        {
            SelectedOptionMatchCount = OptionMatchCounts[TypedValue];
        };
    }

    public override void IncrementOptionMatchCount(OngekiObjectBase obj)
    {
        foreach (var v in Enum.GetValues(typeof(T)).Cast<T>()) {
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
        foreach (var k in OptionMatchCounts.Keys) {
            OptionMatchCounts[k] = 0;
        }

        SelectedOptionMatchCount = 0;
    }

    public override FilterOptionResult Filter(OngekiObjectBase obj)
    {
        return Predicate(obj, TypedValue);
    }
}

public class HeadTailSpecificationOption<THead, TTail> : EnumSpecificationOption<HeadTailSpecification>
    where THead : OngekiObjectBase, ISelectableObject
    where TTail : OngekiObjectBase, ISelectableObject
{
    public delegate THead HeadGetter(TTail obj);
    public delegate TTail TailGetter(THead obj);

    public HeadTailSpecificationOption(string text,
        HeadGetter headGetter,
        TailGetter tailGetter,
        Dictionary<HeadTailSpecification, string>? selectionsText = null)
        : base(text, GetPredicate(headGetter, tailGetter), selectionsText ?? FilterEnumExtensions.HeadTailSpecificationMapStartEnd.ToDictionary())
    { }

    private static FilterPredicate GetPredicate(HeadGetter headGetter, TailGetter tailGetter)
    {
        return (obj, input) =>
        {
            switch (obj) {
                case THead head:
                {
                    var tailObj = tailGetter(head);
                    switch (input) {
                        case HeadTailSpecification.Head:
                        case HeadTailSpecification.HeadNoChild when tailObj is null || !tailObj.IsSelected:
                        case HeadTailSpecification.HeadWithChild when tailObj.IsSelected:
                            return FilterOptionResult.Match;
                        default:
                            return FilterOptionResult.NoMatch;
                    }
                }
                case TTail tail:
                {
                    var headObj = headGetter(tail);
                    switch (input) {
                        case HeadTailSpecification.Tail:
                        case HeadTailSpecification.TailNoParent when !headObj.IsSelected:
                        case HeadTailSpecification.TailWithParent when headObj.IsSelected:
                            return FilterOptionResult.Match;
                        default:
                            return FilterOptionResult.NoMatch;
                    }
                }
                default:
                    return FilterOptionResult.NotApplicable;
            }
        };
    }
}

public sealed class LaneNodeSpecificationOption(string text) : EnumSpecificationOption<HeadTailSpecification>(text, LaneNodePredicate, SelectionsTextMap)
{
    private static readonly Dictionary<HeadTailSpecification, string> SelectionsTextMap = new()
    {
        [HeadTailSpecification.Head] = Resources.SelectionFilter_HeadTailLaneNode_Head,
        [HeadTailSpecification.Tail] = Resources.SelectionFilter_HeadTailLaneNode_Tail,
        [HeadTailSpecification.HeadWithChild] = Resources.SelectionFilter_HeadTailLaneNode_HeadWithChild,
        [HeadTailSpecification.HeadNoChild] = Resources.SelectionFilter_HeadTailLaneNode_HeadNoChild,
        [HeadTailSpecification.TailWithParent] = Resources.SelectionFilter_HeadTailLaneNode_TailWithParent,
        [HeadTailSpecification.TailNoParent] = Resources.SelectionFilter_HeadTailLaneNode_TailNoParent,
    };

    private static FilterOptionResult LaneNodePredicate(OngekiObjectBase obj, HeadTailSpecification input)
    {
        switch (obj) {
            case ConnectableStartObject startObj:
                switch (input) {
                    case HeadTailSpecification.Head:
                    case HeadTailSpecification.HeadNoChild when !startObj.Children.Any(c => c.IsSelected):
                    case HeadTailSpecification.HeadWithChild when startObj.Children.Any(c => c.IsSelected):
                        return FilterOptionResult.Match;
                }
                return FilterOptionResult.NoMatch;
            case ConnectableChildObjectBase childObj:
                switch (input) {
                    case HeadTailSpecification.Tail:
                    case HeadTailSpecification.TailNoParent when !childObj.ReferenceStartObject.IsSelected:
                    case HeadTailSpecification.TailWithParent when childObj.ReferenceStartObject.IsSelected:
                        return FilterOptionResult.Match;
                }
                return FilterOptionResult.NoMatch;
            default:
                return FilterOptionResult.NotApplicable;
        }
    }
}

public sealed class BulletPaletteFilterOption : SelectionFilterOption
{
    public ObservableCollection<Item> Items { get; } = new();
    private Dictionary<BulletPallete, Item> PaletteTable = new();

    private Item NullPaletteItem;

    public int FilterMatches
    {
        get;
        set => Set(ref field, value);
    }

    public BulletPaletteFilterOption(string text)
        : base(text)
    {
        OptionValueChanged += UpdateFilterMatches;

        PropertyChangedEventHandler handler = (_, propChange) =>
        {
            if (propChange.PropertyName == nameof(Item.IsSelected)) {
                NotifyOptionValueChanged();
            }
        };

        Items.CollectionChanged += (_, e) =>
        {
            foreach (var i in e.NewItems?.Cast<Item>() ?? []) {
                i.PropertyChanged += handler;
            }
            foreach (var i in e.OldItems?.Cast<Item>() ?? []) {
                i.PropertyChanged -= handler;
            }
        };

        NullPaletteItem = new Item(null);
    }

    public void FumenLoaded(OngekiFumen fumen)
    {
        fumen.BulletPalleteList.CollectionChanged += BulletPaletteCollectionChanged;
        UpdateOptions(fumen.BulletPalleteList);
    }

    public void FumenUnloaded(OngekiFumen fumen)
    {
        fumen.BulletPalleteList.CollectionChanged -= BulletPaletteCollectionChanged;
    }

    public void UpdateOptions(BulletPalleteList paletteList)
    {
        Items.Clear();

        NullPaletteItem = new Item(null);
        Items.Add(NullPaletteItem);
        Items.Add(new(BulletPallete.DummyCustomPallete));
        Items.AddRange(paletteList.Select(p => new Item(p)));

        PaletteTable = Items.Except([NullPaletteItem]).ToDictionary(i => i.Palette!, i => i);
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
        if (selectedPalettes.IsEmpty()) {
            return bullet.ReferenceBulletPallete == null ? FilterOptionResult.Match : FilterOptionResult.NoMatch;
        }

        return selectedPalettes.Any(i => i.Palette == bullet.ReferenceBulletPallete) ? FilterOptionResult.Match : FilterOptionResult.NoMatch;
    }

    public override void IncrementOptionMatchCount(OngekiObjectBase obj)
    {
        if (obj is not IBulletPalleteReferencable bullet)
            return;

        var item = bullet.ReferenceBulletPallete == null
            ? NullPaletteItem
            : PaletteTable[bullet.ReferenceBulletPallete];

        if (bullet is Bullet)
            item.BulletCount++;
        else if (bullet is Bell)
            item.BellCount++;

        UpdateFilterMatches();
    }

    public override void ResetOptionMatchCount()
    {
        foreach (var palette in Items) {
            palette.BulletCount = 0;
            palette.BellCount = 0;
        }

        FilterMatches = 0;
    }

    private void UpdateFilterMatches()
    {
        FilterMatches = Items.Where(i => i.IsSelected).Sum(i => i.BulletCount + i.BellCount);
    }

    public class Item(BulletPallete? palette) : PropertyChangedBase
    {
        public BulletPallete? Palette { get; } = palette;

        public bool IsSelected
        {
            get;
            set => Set(ref field, value);
        }

        public int BulletCount
        {
            get;
            set
            {
                Set(ref field, value);
                NotifyOfPropertyChange(nameof(Text));
            }
        }

        public int BellCount
        {
            get;
            set
            {
                Set(ref field, value);
                NotifyOfPropertyChange(nameof(Text));
            }
        }

        public string Text
        {
            get
            {
                var baseText = Palette is null
                    ? Resources.NoBulletPalette
                    : Palette == BulletPallete.DummyCustomPallete
                        ? Palette.EditorName
                        : $"{Palette.StrID} {Palette.EditorName}";
                return $"{baseText} ({BulletCount} | {BellCount})";
            }
        }
    }
}

public sealed class DockableObjectLaneFilterOption : SelectionFilterOption
{
    public ObservableCollection<Item> Values { get; } = new();

    public int FilterMatches
    {
        get;
        set => Set(ref field, value);
    }

    public DockableObjectLaneFilterOption(string text) : base(text)
    {
        Values.AddRange(Enum.GetValues<DockableTargetSpecification>().Select(d => new Item(d)));

        foreach (var v in Values) {
            v.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(Item.IsSelected)) {
                    NotifyOptionValueChanged();
                    UpdateFilterMatches();
                }
            };
        }
    }

    public override FilterOptionResult Filter(OngekiObjectBase obj)
    {
        if (obj is not ILaneDockable dockable)
            return FilterOptionResult.NotApplicable;

        return GetItemFromObject(dockable) is { IsSelected: true }
            ? FilterOptionResult.Match : FilterOptionResult.NoMatch;
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

    private Item? GetItemFromObject(ILaneDockable dockable)
        => Values.SingleOrDefault(i => i.DockLane == (dockable.ReferenceLaneStart?.LaneType ?? LaneType.Undefined).GetDockableTargetSpecification());

    public override void ResetOptionMatchCount()
    {
        foreach (var item in Values)
            item.MatchCount = 0;
        FilterMatches = 0;
    }

    public class Item : PropertyChangedBase
    {
        public DockableTargetSpecification DockLane { get; }

        public int MatchCount
        {
            get;
            set
            {
                Set(ref field, value);
                NotifyOfPropertyChange(nameof(Text));
            }
        }

        public bool IsSelected
        {
            get;
            set => Set(ref field, value);
        }

        public Item(DockableTargetSpecification dockLane)
        {
            DockLane = dockLane;
        }

        public string Text => $"{DockLane.ToResourceName()} ({MatchCount})";
    }
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
    public static readonly ImmutableDictionary<HeadTailSpecification, string> HeadTailSpecificationMapStartEnd = new Dictionary<HeadTailSpecification, string>
    {
        [HeadTailSpecification.Head] = Resources.SelectionFilter_HeadTailHoldObject_Head,
        [HeadTailSpecification.Tail] = Resources.SelectionFilter_HeadTailHoldObject_Tail,
        [HeadTailSpecification.HeadWithChild] = Resources.SelectionFilter_HeadTailHoldObject_HeadWithChild,
        [HeadTailSpecification.HeadNoChild] = Resources.SelectionFilter_HeadTailHoldObject_HeadNoChild,
        [HeadTailSpecification.TailWithParent] = Resources.SelectionFilter_HeadTailHoldObject_TailWithParent,
        [HeadTailSpecification.TailNoParent] = Resources.SelectionFilter_HeadTailHoldObject_TailNoParent,
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
            DockableTargetSpecification.NoLane => Resources.SelectionFilter_None,
            DockableTargetSpecification.WallLeft => Resources.WallLeft,
            DockableTargetSpecification.WallRight => Resources.WallRight,
            DockableTargetSpecification.LaneLeft => Resources.LaneLeft,
            DockableTargetSpecification.LaneCenter => Resources.LaneCenter,
            DockableTargetSpecification.LaneRight => Resources.LaneRight,
            _ => throw new ArgumentOutOfRangeException(nameof(spec), spec, null)
        };
    }

    public static bool ToBool(this SelectionStatusSpecification spec)
        => spec switch
        {
            SelectionStatusSpecification.Selected => true,
            SelectionStatusSpecification.Unselected => false,
            _ => throw new ArgumentOutOfRangeException(nameof(spec), spec, null)
        };
}