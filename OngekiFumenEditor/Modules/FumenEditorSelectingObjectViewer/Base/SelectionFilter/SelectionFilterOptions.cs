using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Caliburn.Micro;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Base.OngekiObjects.Lane;
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

    private bool _isEnabled;
    public bool IsEnabled
    {
        get => _isEnabled;
        set => Set(ref _isEnabled, value);
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

    private bool _isRegex;
    private string _inputText;

    public bool IsRegex
    {
        get => _isRegex;
        set
        {
            Set(ref _isRegex, value);
            NotifyOptionValueChanged();
        }
    }

    public string InputText
    {
        get => _inputText;
        set
        {
            Set(ref _inputText, value);
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
    private object? _value = null;

    public readonly Func<OngekiObjectBase, T, bool> Predicate;

    protected SelectionFilterOption(string text, Func<OngekiObjectBase, T, bool> predicate)
        : base(text)
    {
        Predicate = predicate;
    }

    public object? Value
    {
        get => _value;
        set
        {
            if (value is not T and not null) {
                throw new InvalidOperationException();
            }

            Set(ref _value, value);
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
    private string _falseText;
    private string _trueText;

    public string FalseText
    {
        get => _falseText;
        set => Set(ref _falseText, value);
    }

    public string TrueText
    {
        get => _trueText;
        set => Set(ref _trueText, value);
    }

    public BooleanOption(string text, Func<OngekiObjectBase, bool, bool> filter)
        : base(text, filter)
    {
        base.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(IsEnabled)) {
                if (IsEnabled)
                    Value = true;
                else
                    Value = null;
            }
        };
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

    private object _value;
    public object Value
    {
        get => _value;
        set
        {
            if (value.GetType() != EnumType)
                throw new InvalidOperationException();
            Set(ref _value, value);
            NotifyOptionValueChanged();
        }
    }
}

public abstract class EnumSpecificationOption<T>(string text) : EnumSpecificationOption(text) where T : Enum
{
    protected sealed override Type EnumType => typeof(T);
    public sealed override object[] Selections => Enum.GetValues(typeof(T)).Cast<object>().ToArray();
}

public class LaneNodeSpecificationOption(string text) : EnumSpecificationOption<HeadTailSpecification>(text)
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

public class BulletPaletteFilterOption : SelectionFilterOption
{
    public ObservableCollection<Item> FumenPalettes { get; } = new();
    public ObservableCollection<Item> SelectedPalettes { get; } = new();
    private Dictionary<BulletPallete, Item> PaletteTable = new();

    public BulletPaletteFilterOption(string text)
        : base(text)
    {
        SelectedPalettes.CollectionChanged += (s, e) =>
        {
            NotifyOptionValueChanged();
        };
    }

    public void UpdateOptions(OngekiFumen fumen)
    {
        SelectedPalettes.Clear();
        FumenPalettes.Clear();
        FumenPalettes.AddRange(fumen.BulletPalleteList.Select(p => new Item(p)));

        PaletteTable = FumenPalettes.ToDictionary(i => i.Palette, i => i);
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

        if (SelectedPalettes.IsEmpty()) {
            return bullet.ReferenceBulletPallete == null;
        }

        return SelectedPalettes.Any(i => i.Palette == bullet.ReferenceBulletPallete);
    }

    public class Item : PropertyChangedBase
    {
        public string Text => $"{Palette.StrID} {Palette.EditorName} ({BulletObjects.Count} | {BellObjects.Count})";

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

public class DockableObjectLaneFilterOption : SelectionFilterOption
{
    public IEnumerable<DockableTargetSpecification> AllValues => Enum.GetValues<DockableTargetSpecification>();
    public ObservableCollection<DockableTargetSpecification> SelectedTargets { get; } = new();

    public DockableObjectLaneFilterOption(string text) : base(text)
    {
        SelectedTargets.CollectionChanged += (_, _) => NotifyOptionValueChanged();
    }

    public override bool Filter(OngekiObjectBase obj)
    {
        if (obj is not ILaneDockable dockable)
            return true;

        return SelectedTargets.Any(t => t.GetLaneType() == dockable.ReferenceLaneStart.LaneType);
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