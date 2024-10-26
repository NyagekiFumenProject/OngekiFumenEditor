#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Caliburn.Micro;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Base.OngekiObjects.Lane;
using OngekiFumenEditor.Base.OngekiObjects.Lane.Base;
using OngekiFumenEditor.Base.OngekiObjects.Wall;
using OngekiFumenEditor.Modules.FumenVisualEditor.Kernel;
using OngekiFumenEditor.Properties;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Behaviors.BatchMode;

/// <summary>
/// Represents a sub-mode of the Batch Mode.
/// The sub-mode determines the behavior when clicking and holding modifiers.
/// Sub-modes are selected via Batch Mode shortcuts.
/// Only one sub-mode can be active at a time.
/// </summary>
public abstract class BatchModeSubmode
{
    public abstract string DisplayName { get; }
}

/// <summary>
/// A sub-mode that controls what the user is able to select.
/// </summary>
public abstract class BatchModeFilterSubmode : BatchModeSubmode
{
    public abstract Func<OngekiObjectBase, bool> FilterFunction { get; }
}

public abstract class BatchModeInputSubmode : BatchModeSubmode
{
    public abstract IEnumerable<OngekiTimelineObjectBase> GenerateObject();
    public virtual bool AutoSelect => false;
    public virtual BatchModeObjectModificationAction? ModifyObjectCtrl { get; } = null;
    public virtual BatchModeObjectModificationAction? ModifyObjectShift =>
        AutoSelect
            ? new BatchModeObjectModificationAction(null, Resources.BatchModeModifierAddToSelection)
            : null;
}

public class BatchModeInputClipboard : BatchModeInputSubmode
{
    private IFumenEditorClipboard Clipboard;

    public BatchModeInputClipboard()
    {
        Clipboard = IoC.Get<IFumenEditorClipboard>();
    }

    public override string DisplayName => Resources.Clipboard;
    public override IEnumerable<OngekiTimelineObjectBase> GenerateObject()
    {
        return Clipboard.CurrentCopiedObjects.Select(obj => (OngekiTimelineObjectBase)obj.CopyNew());
    }
}

public abstract class BatchModeSingleInputSubmode : BatchModeInputSubmode
{
    public abstract Type ObjectType { get;}
}

public abstract class BatchModeInputSubmode<T> : BatchModeSingleInputSubmode
    where T : OngekiTimelineObjectBase
{
    public override Type ObjectType => typeof(T);

    public override IEnumerable<OngekiTimelineObjectBase> GenerateObject()
    {
        yield return Activator.CreateInstance<T>();
    }
}

public abstract class BatchModeInputLane<T> : BatchModeInputSubmode<T>
    where T : LaneStartBase, new()
{
    public override bool AutoSelect => true;
}

public class BatchModeInputLaneLeft : BatchModeInputLane<LaneLeftStart>
{
    public override string DisplayName => Resources.LaneLeft;
}

public class BatchModeInputLaneCenter : BatchModeInputLane<LaneCenterStart>
{
    public override string DisplayName => Resources.LaneCenter;
}

public class BatchModeInputLaneRight : BatchModeInputLane<LaneRightStart>
{
    public override string DisplayName => Resources.LaneRight;
}

public class BatchModeInputWallRight : BatchModeInputLane<WallRightStart>
{
    public override string DisplayName => Resources.WallRight;
}

public class BatchModeInputWallLeft : BatchModeInputLane<WallLeftStart>
{
    public override string DisplayName => Resources.WallLeft;
}

public class BatchModeInputLaneColorful : BatchModeInputLane<ColorfulLaneStart>
{
    public override string DisplayName => Resources.LaneColorful;
}

public abstract class BatchModeInputHitSubmode<T> : BatchModeInputSubmode<T>
    where T : OngekiTimelineObjectBase, ICriticalableObject
{
    public override BatchModeObjectModificationAction ModifyObjectCtrl { get; } = new(CritObject, Resources.BatchModeModifierSetCritical);

    private static void CritObject(OngekiObjectBase baseObject)
    {
        ((ICriticalableObject)baseObject).IsCritical = true;
    }
}

public class BatchModeInputTap : BatchModeInputHitSubmode<Tap>
{
    public override string DisplayName => Resources.Tap;
}

public class BatchModeInputHold : BatchModeInputHitSubmode<Hold>
{
    public override string DisplayName => Resources.Hold;
    public override bool AutoSelect => true;
}

public class BatchModeInputFlick : BatchModeInputHitSubmode<Flick>
{
    public override BatchModeObjectModificationAction ModifyObjectShift { get; } = new(SwitchFlick, Resources.BatchModeModifierSwitchDirection);

    private static void SwitchFlick(OngekiObjectBase baseObject)
    {
        ((Flick)baseObject).Direction = Flick.FlickDirection.Right;
    }

    public override string DisplayName => Resources.Flick;
}

public class BatchModeInputLaneBlock : BatchModeInputSubmode<LaneBlockArea>
{
    public override BatchModeObjectModificationAction ModifyObjectCtrl { get; } =
        new BatchModeObjectModificationAction(SwitchDirection, Resources.BatchModeModifierSwitchDirection);

    private static void SwitchDirection(OngekiObjectBase baseObject)
    {
        ((LaneBlockArea)baseObject).Direction = LaneBlockArea.BlockDirection.Right;
    }

    public override string DisplayName => Resources.LaneBlock;
}

public class BatchModeInputNormalBell : BatchModeInputSubmode<Bell>
{
    public override string DisplayName => Resources.Bell;
}

public class BatchModeFilterLanes : BatchModeFilterSubmode
{
    public override string DisplayName => Resources.ObjectFilterLanes;
    public override Func<OngekiObjectBase, bool> FilterFunction => obj => obj is LaneStartBase or LaneNextBase;
}

public class BatchModeFilterDockableObjects : BatchModeFilterSubmode
{
    public override string DisplayName => Resources.ObjectFilterDockables;
    public override Func<OngekiObjectBase, bool> FilterFunction => obj => obj is Tap or Hold or HoldEnd;
}

public class BatchModeFilterFloatingObjects : BatchModeFilterSubmode
{
    public override string DisplayName => Resources.ObjectFilterFloating;
    public override Func<OngekiObjectBase, bool> FilterFunction => obj => obj is Bell or Bullet or Flick;
}

public class BatchModeObjectModificationAction(Action<OngekiObjectBase>? modifier, string description)
{
    public string Description { get; } = description;
    public Action<OngekiObjectBase>? Function { get; } = modifier;
}

