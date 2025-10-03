using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using Caliburn.Micro;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.Collections;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Modules.FumenEditorSelectingObjectViewer.ValueConverters;
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

    public abstract bool Filter(OngekiObjectBase obj);

    protected void NotifyOptionValueChanged()
    {
        OptionValueChanged?.Invoke();
    }

    public virtual void OnSelectionRefreshed(IEnumerable<ISelectableObject> obj) { }
}

public class TextWithRegexOption : SelectionFilterOption
{
    public delegate bool FilterPredicate(OngekiObjectBase obj, string input, bool filterIsEnabled);

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

    public FilterPredicate Predicate { get; }

    public TextWithRegexOption(string text, FilterPredicate filter) : base(text)
    {
        Predicate = filter;
    }

    public override bool Filter(OngekiObjectBase obj)
    {
        return Predicate(obj, InputText, IsRegex);
    }
}

public abstract class SelectionFilterOption<T> : SelectionFilterOption
{
    public readonly Func<OngekiObjectBase, T, bool> Predicate;

    protected SelectionFilterOption(string text, Func<OngekiObjectBase, T, bool> predicate)
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

    public sealed override bool Filter(OngekiObjectBase obj)
    {
        return Predicate(obj, (T)Value);
    }
}

public class BooleanOption : SelectionFilterOption<bool>
{
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

    public BooleanOption(string text, Func<OngekiObjectBase, bool, bool> filter)
        : base(text, filter)
    {
        Value = true;
    }

    public static BooleanOption YesNoOption(string text, Func<OngekiObjectBase, bool, bool> filter)
    {
        return new(text, filter)
        {
            TrueText = Resources.SelectionFilter_ChoiceYes,
            FalseText = Resources.SelectionFilter_ChoiceNo
        };
    }

    public static BooleanOption LeftRightOption(string text, Func<OngekiObjectBase, bool, bool> filter)
    {
        return new(text, filter)
        {
            TrueText = Resources.DirectionLeft,
            FalseText = Resources.DirectionRight
        };
    }
}

public abstract class EnumSpecificationOption(string text) : SelectionFilterOption(text)
{
    protected abstract Type EnumType { get; }

    public abstract object[] Selections { get; }
    public abstract Dictionary<object, string> SelectionsText { get; }

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

public abstract class EnumSpecificationOption<T> : EnumSpecificationOption where T : Enum
{
    public EnumSpecificationOption(string text) : base(text)
    {
        Value = default(T);
    }

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

    public override bool Filter(OngekiObjectBase obj)
    {
        switch (obj) {
            case ConnectableStartObject startObj:
                switch (Value) {
                    case HeadTailSpecification.Head:
                    case HeadTailSpecification.HeadNoChild when !startObj.Children.Any(c => c.IsSelected):
                    case HeadTailSpecification.HeadWithChild when startObj.Children.Any(c => c.IsSelected):
                        return true;
                }
                return false;
            case ConnectableChildObjectBase childObj:
                switch (Value) {
                    case HeadTailSpecification.Tail:
                    case HeadTailSpecification.TailNoParent when !childObj.ReferenceStartObject.IsSelected:
                    case HeadTailSpecification.TailWithParent when childObj.ReferenceStartObject.IsSelected:
                        return true;
                }
                return false;
            default:
                return true;
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

    public override bool Filter(OngekiObjectBase obj)
    {
        switch (obj) {
            case Hold hold:
                switch (Value) {
                    case HeadTailSpecification.Head:
                    case HeadTailSpecification.HeadNoChild
                        when hold.HoldEnd is null || !hold.HoldEnd.IsSelected:
                    case HeadTailSpecification.HeadWithChild when hold.HoldEnd.IsSelected:
                        return true;
                }
                return false;
            case HoldEnd holdEnd:
                switch (Value) {
                    case HeadTailSpecification.Tail:
                    case HeadTailSpecification.TailNoParent when !holdEnd.RefHold.IsSelected:
                    case HeadTailSpecification.TailWithParent when holdEnd.RefHold.IsSelected:
                        return true;
                }
                return false;
            default:
                return true;
        }
    }
}

public sealed class BulletPaletteFilterOption : SelectionFilterOption
{
    public ObservableCollection<Item> FumenPalettes { get; } = new();
    private Dictionary<BulletPallete, Item> PaletteTable = new();

    public string ValueText
    {
        get
        {
            var selectedPalettes = FumenPalettes.Where(p => p.IsSelected).ToArray();
            return $"({selectedPalettes.Length}) {string.Join(", ", selectedPalettes.Select(p => p.Palette.StrID))}";
        }
    }

    public BulletPaletteFilterOption(string text)
        : base(text)
    {
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

    public override void OnSelectionRefreshed(IEnumerable<ISelectableObject> selection)
    {
        if (FumenPalettes.IsEmpty())
            return;

        foreach (var palette in FumenPalettes) {
            palette.BulletObjects.Clear();
            palette.BellObjects.Clear();
        }

        foreach (var obj in selection) {
            if (obj is Bullet bullet && PaletteTable.TryGetValue(bullet.ReferenceBulletPallete, out var bplItem))
                bplItem.BulletObjects.Add(bullet);
            else if (obj is Bell { ReferenceBulletPallete: not null } bell && PaletteTable.TryGetValue(bell.ReferenceBulletPallete, out var bplItem2))
                bplItem2.BellObjects.Add(bell);
        }
    }

    public override bool Filter(OngekiObjectBase obj)
    {
        if (obj is not IBulletPalleteReferencable bullet)
            return true;

        var selectedPalettes = FumenPalettes.Where(p => p.IsSelected).ToArray();
        if (selectedPalettes.IsEmpty()) {
            return bullet.ReferenceBulletPallete == null;
        }

        return selectedPalettes.Any(i => i.Palette == bullet.ReferenceBulletPallete);
    }

    public class Item : PropertyChangedBase
    {
        public string Text => $"{Palette.StrID} {Palette.EditorName} ({BulletObjects.Count} | {BellObjects.Count})";

        public bool IsSelected
        {
            get;
            set => Set(ref field, value);
        }

        public Item(BulletPallete palette)
        {
            Palette = palette;
            BulletObjects.CollectionChanged += (_, _) => NotifyOfPropertyChange(nameof(Text));
            BellObjects.CollectionChanged += (_, _) => NotifyOfPropertyChange(nameof(Text));
        }

        public BulletPallete Palette { get; }
        public ObservableCollection<IBulletPalleteReferencable> BulletObjects { get; } = new();
        public ObservableCollection<IBulletPalleteReferencable> BellObjects { get; } = new();
    }
}

public sealed class DockableObjectLaneFilterOption : SelectionFilterOption
{
    public ObservableCollection<Item> AllValues { get; } = new();

    public DockableObjectLaneFilterOption(string text) : base(text)
    {
        AllValues.AddRange(Enum.GetValues<DockableTargetSpecification>().Select(d => new Item(d)));
    }

    public override bool Filter(OngekiObjectBase obj)
    {
        if (obj is not ILaneDockable dockable)
            return true;

        return AllValues.Where(v => v.IsSelected).Any(t => t.DockLane.GetLaneType() == dockable.ReferenceLaneStart.LaneType);
    }

    public class Item : PropertyChangedBase
    {
        public DockableTargetSpecification DockLane { get; }

        public Item(DockableTargetSpecification dockLane)
        {
            DockLane = dockLane;
        }

        public bool IsSelected
        {
            get;
            set => Set(ref field, value);
        }

        public string Text => DockLane.ToString();
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
}