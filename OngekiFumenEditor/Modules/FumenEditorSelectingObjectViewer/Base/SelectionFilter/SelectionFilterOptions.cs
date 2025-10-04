using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using AngleSharp.Dom;
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

    public OptionCategory(string name, IEnumerable<SelectionFilterOption> options)
    {
        Options.AddRange(options);
        Name = name;
    }
}

/// <summary>
/// Base class for any filter option.
/// </summary>
public abstract class SelectionFilterOption : PropertyChangedBase
{
    public delegate void OptionValueChangedEventHandler();
    public event OptionValueChangedEventHandler OptionValueChanged;

    public bool IsEnabled
    {
        get;
        set => Set(ref field, value);
    }

    public string Text { get; }

    public SelectionFilterOption(string text)
    {
        Text = text;
    }

    public abstract FilterOptionResult Filter(OngekiObjectBase obj);

    public abstract void UpdateFilterCounts(OngekiObjectBase obj);
    public abstract void ResetFilterCounts();

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
    }

    public string InputText
    {
        get;
        set
        {
            Set(ref field, value);
            NotifyOptionValueChanged();
        }
    }

    public int MatchCount
    {
        get;
        set => Set(ref field, value);
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

    public override void UpdateFilterCounts(OngekiObjectBase obj)
    {
        if (Filter(obj) == FilterOptionResult.Match)
            MatchCount++;
    }

    public override void ResetFilterCounts()
    {
        MatchCount = 0;
    }
}

/// <summary>
/// Base class for single-value filter options.
/// </summary>
public abstract class SelectionFilterOption<T> : SelectionFilterOption
{
    public readonly Func<OngekiObjectBase, T, FilterOptionResult> Predicate;

    protected SelectionFilterOption(string text, Func<OngekiObjectBase, T, FilterOptionResult> predicate)
        : base(text)
    {
        Predicate = predicate;
    }

    public object Value
    {
        get;
        set
        {
            if (value is not T and not null) {
                throw new InvalidOperationException();
            }

            Set(ref field, value);
            NotifyOptionValueChanged();
        }
    }

    public sealed override FilterOptionResult Filter(OngekiObjectBase obj)
    {
        return Predicate(obj, (T)Value);
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
        set => Set(ref field, value);
    }

    public int TrueMatches
    {
        get;
        set => Set(ref field, value);
    }

    public string FalseText
    {
        get;
        init => Set(ref field, value);
    }

    public string TrueText
    {
        get;
        init => Set(ref field, value);
    }

    public BooleanOption(string text, Func<OngekiObjectBase, bool, FilterOptionResult> filter)
        : base(text, filter)
    {
        Value = true;
    }

    public override void UpdateFilterCounts(OngekiObjectBase obj)
    {
        if (Predicate(obj, true) == FilterOptionResult.Match) {
            TrueMatches++;
        }

        if (Predicate(obj, false) == FilterOptionResult.Match) {
            FalseMatches++;
        }
    }

    public override void ResetFilterCounts()
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
public abstract class EnumSpecificationOption(string text) : SelectionFilterOption(text)
{
    protected abstract Type EnumType { get; }

    public abstract object[] Selections { get; }
    public abstract Dictionary<object, string> SelectionsText { get; }
    public abstract int TotalMatches { get; set; }

    public object Value
    {
        get;
        set
        {
            if (value.GetType() != EnumType)
                throw new InvalidOperationException();
            Set(ref field, value);
            NotifyOptionValueChanged();
        }
    }
}

/// <inheritdoc />
public abstract class EnumSpecificationOption<T> : EnumSpecificationOption where T : Enum
{
    public Dictionary<T, int> MatchCounts { get; } = Enum.GetValues(typeof(T)).Cast<T>().ToDictionary(x => x, x => 0);

    public override int TotalMatches
    {
        get;
        set => Set(ref field, value);
    } = 0;

    public EnumSpecificationOption(string text) : base(text)
    {
        Value = default(T);
        OptionValueChanged += () =>
        {
            TotalMatches = MatchCounts[(T)Value];
        };
    }

    public override void UpdateFilterCounts(OngekiObjectBase obj)
    {
        foreach (var v in Enum.GetValues(typeof(T)).Cast<T>()) {
            var res = Predicate(v, obj);
            if (res == FilterOptionResult.NotApplicable)
                break;
            if (res == FilterOptionResult.Match)
                MatchCounts[v]++;
        }

        TotalMatches = MatchCounts.ContainsKey((T)Value) ? MatchCounts[(T)Value] : 0;
    }

    public override void ResetFilterCounts()
    {
        foreach (var k in MatchCounts.Keys) {
            MatchCounts[k] = 0;
        }

        TotalMatches = 0;
    }

    public override FilterOptionResult Filter(OngekiObjectBase obj)
    {
        return Predicate((T)Value, obj);
    }

    protected abstract FilterOptionResult Predicate(T input, OngekiObjectBase obj);

    protected sealed override Type EnumType => typeof(T);
    public sealed override object[] Selections => Enum.GetValues(typeof(T)).Cast<object>().ToArray();
}

public sealed class LaneNodeSpecificationOption(string text) : EnumSpecificationOption<HeadTailSpecification>(text)
{
    private static readonly Dictionary<object, string> _selectionsText = new()
    {
        [HeadTailSpecification.Head] = Resources.SelectionFilter_HeadTailLaneNode_Head,
        [HeadTailSpecification.Tail] = Resources.SelectionFilter_HeadTailLaneNode_Tail,
        [HeadTailSpecification.HeadWithChild] = Resources.SelectionFilter_HeadTailLaneNode_HeadWithChild,
        [HeadTailSpecification.HeadNoChild] = Resources.SelectionFilter_HeadTailLaneNode_HeadNoChild,
        [HeadTailSpecification.TailNoParent] = Resources.SelectionFilter_HeadTailLaneNode_TailNoParent,
        [HeadTailSpecification.TailWithParent] = Resources.SelectionFilter_HeadTailLaneNode_TailWithParent,
    };

    public override Dictionary<object, string> SelectionsText => _selectionsText;

    protected override FilterOptionResult Predicate(HeadTailSpecification input, OngekiObjectBase obj)
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

public sealed class HoldObjectSpecificationOption(string text) : EnumSpecificationOption<HeadTailSpecification>(text)
{
    private static readonly Dictionary<object, string> _selectionsText = new()
    {
        [HeadTailSpecification.Head] = Resources.SelectionFilter_HeadTailHoldObject_Head,
        [HeadTailSpecification.Tail] = Resources.SelectionFilter_HeadTailHoldObject_Tail,
        [HeadTailSpecification.HeadWithChild] = Resources.SelectionFilter_HeadTailHoldObject_HeadWithChild,
        [HeadTailSpecification.HeadNoChild] = Resources.SelectionFilter_HeadTailHoldObject_HeadNoChild,
        [HeadTailSpecification.TailNoParent] = Resources.SelectionFilter_HeadTailHoldObject_TailNoParent,
        [HeadTailSpecification.TailWithParent] = Resources.SelectionFilter_HeadTailHoldObject_TailWithParent,
    };

    public override Dictionary<object, string> SelectionsText => _selectionsText;

    protected override FilterOptionResult Predicate(HeadTailSpecification input, OngekiObjectBase obj)
    {
        switch (obj) {
            case Hold hold:
                switch (Value) {
                    case HeadTailSpecification.Head:
                    case HeadTailSpecification.HeadNoChild
                        when hold.HoldEnd is null || !hold.HoldEnd.IsSelected:
                    case HeadTailSpecification.HeadWithChild when hold.HoldEnd.IsSelected:
                        return FilterOptionResult.Match;
                }
                return FilterOptionResult.NoMatch;
            case HoldEnd holdEnd:
                switch (Value) {
                    case HeadTailSpecification.Tail:
                    case HeadTailSpecification.TailNoParent when !holdEnd.RefHold.IsSelected:
                    case HeadTailSpecification.TailWithParent when holdEnd.RefHold.IsSelected:
                        return FilterOptionResult.Match;
                }
                return FilterOptionResult.NoMatch;
            default:
                return FilterOptionResult.NotApplicable;
        }
    }
}

public sealed class LaneBlockSpecificationOption(string text) : EnumSpecificationOption<HeadTailSpecification>(text)
{
    private static readonly Dictionary<object, string> _selectionsText = new()
    {
        [HeadTailSpecification.Head] = Resources.SelectionFilter_HeadTailHoldObject_Head,
        [HeadTailSpecification.Tail] = Resources.SelectionFilter_HeadTailHoldObject_Tail,
        [HeadTailSpecification.HeadWithChild] = Resources.SelectionFilter_HeadTailHoldObject_HeadWithChild,
        [HeadTailSpecification.HeadNoChild] = Resources.SelectionFilter_HeadTailHoldObject_HeadNoChild,
        [HeadTailSpecification.TailNoParent] = Resources.SelectionFilter_HeadTailHoldObject_TailNoParent,
        [HeadTailSpecification.TailWithParent] = Resources.SelectionFilter_HeadTailHoldObject_TailWithParent,
    };

    public override Dictionary<object, string> SelectionsText => _selectionsText;

    protected override FilterOptionResult Predicate(HeadTailSpecification input, OngekiObjectBase obj)
    {
        switch (obj) {
            case LaneBlockArea hold:
                switch (Value) {
                    case HeadTailSpecification.Head:
                    case HeadTailSpecification.HeadNoChild
                        when hold.EndIndicator is null || !hold.EndIndicator.IsSelected:
                    case HeadTailSpecification.HeadWithChild when hold.EndIndicator.IsSelected:
                        return FilterOptionResult.Match;
                }
                return FilterOptionResult.NoMatch;
            case LaneBlockArea.LaneBlockAreaEndIndicator holdEnd:
                switch (Value) {
                    case HeadTailSpecification.Tail:
                    case HeadTailSpecification.TailNoParent when !holdEnd.RefLaneBlockArea.IsSelected:
                    case HeadTailSpecification.TailWithParent when holdEnd.RefLaneBlockArea.IsSelected:
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
    public ObservableCollection<Item> FumenPalettes { get; } = new();
    private Dictionary<BulletPallete, Item> PaletteTable = new();

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

        FumenPalettes.CollectionChanged += (s, e) =>
        {
            foreach (var i in e.NewItems?.Cast<Item>() ?? []) {
                i.PropertyChanged += handler;
            }
            foreach (var i in e.OldItems?.Cast<Item>() ?? []) {
                i.PropertyChanged -= handler;
            }
        };
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
        FumenPalettes.Clear();
        FumenPalettes.AddRange(paletteList.Select(p => new Item(p)));

        PaletteTable = FumenPalettes.ToDictionary(i => i.Palette, i => i);
    }

    private void BulletPaletteCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        UpdateOptions((BulletPalleteList)sender);
    }

    public override FilterOptionResult Filter(OngekiObjectBase obj)
    {
        if (obj is not IBulletPalleteReferencable bullet)
            return FilterOptionResult.NotApplicable;

        var selectedPalettes = FumenPalettes.Where(p => p.IsSelected).ToArray();
        if (selectedPalettes.IsEmpty()) {
            return bullet.ReferenceBulletPallete == null ? FilterOptionResult.Match : FilterOptionResult.NoMatch;
        }

        return selectedPalettes.Any(i => i.Palette == bullet.ReferenceBulletPallete) ? FilterOptionResult.Match : FilterOptionResult.NoMatch;
    }

    public override void UpdateFilterCounts(OngekiObjectBase obj)
    {
        if (obj is not IBulletPalleteReferencable bullet)
            return;

        if (bullet.ReferenceBulletPallete == null)
            return;
        if (!PaletteTable.TryGetValue(bullet.ReferenceBulletPallete, out var item))
            return;

        if (bullet is Bullet)
            item.BulletCount++;
        else if (bullet is Bell)
            item.BellCount++;

        UpdateFilterMatches();
    }

    public override void ResetFilterCounts()
    {
        foreach (var palette in FumenPalettes) {
            palette.BulletCount = 0;
            palette.BellCount = 0;
        }

        FilterMatches = 0;
    }

    private void UpdateFilterMatches()
    {
        FilterMatches = FumenPalettes.Where(i => i.IsSelected).Sum(i => i.BulletCount + i.BellCount);
    }

    public class Item(BulletPallete palette) : PropertyChangedBase
    {
        public BulletPallete Palette { get; } = palette;

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

        public string Text => $"{Palette.StrID} {Palette.EditorName} ({BulletCount} | {BellCount})";
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

        return Values.Where(v => v.IsSelected).Any(t => t.DockLane.GetLaneType() == dockable.ReferenceLaneStart.LaneType)
            ? FilterOptionResult.Match : FilterOptionResult.NoMatch;
    }

    public override void UpdateFilterCounts(OngekiObjectBase obj)
    {
        if (obj is not ILaneDockable dockable) {
            return;
        }

        var match = Values.SingleOrDefault(i => i.DockLane == dockable.ReferenceLaneStart.LaneType.GetDockableTargetSpecification());
        if (match is not null) {
            match.MatchCount++;
        }

        UpdateFilterMatches();
    }

    private void UpdateFilterMatches()
    {
        FilterMatches = Values.Where(i => i.IsSelected).Sum(i => i.MatchCount);
    }

    public override void ResetFilterCounts()
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
    WallLeft,
    LaneLeft,
    LaneCenter,
    LaneRight,
    WallRight
}

public enum FilterOptionResult
{
    Match,
    NoMatch,
    NotApplicable
}

public static class FilterEnumExtensions
{
    public static LaneType GetLaneType(this DockableTargetSpecification spec)
    {
        return spec switch
        {
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
            DockableTargetSpecification.WallLeft => Resources.WallLeft,
            DockableTargetSpecification.WallRight => Resources.WallRight,
            DockableTargetSpecification.LaneLeft => Resources.LaneLeft,
            DockableTargetSpecification.LaneCenter => Resources.LaneCenter,
            DockableTargetSpecification.LaneRight => Resources.LaneRight,
            _ => throw new ArgumentOutOfRangeException(nameof(spec), spec, null)
        };
    }
}