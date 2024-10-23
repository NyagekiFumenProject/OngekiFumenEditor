#nullable enable
using System;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Base.OngekiObjects.Lane;
using OngekiFumenEditor.Base.OngekiObjects.Lane.Base;
using OngekiFumenEditor.Base.OngekiObjects.Wall;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Behaviors.BrushMode;

public class ObjectModificationAction(Action<OngekiObjectBase>? modifier, string description)
{
    public string Description { get; } = description;
    public Action<OngekiObjectBase>? Function { get; } = modifier;
}

public abstract class BrushModeInputObject
{
    public abstract string DisplayName { get; }
    public abstract Type ObjectType { get; }
    public abstract OngekiTimelineObjectBase GenerateObject();

    public virtual bool IsKeepExistingSelection => false;

    public virtual ObjectModificationAction? ModifyObjectCtrl { get; } = null;
    public virtual ObjectModificationAction? ModifyObjectShift =>
        IsKeepExistingSelection
            ? new ObjectModificationAction(null, "Add to selection")
            : null;
}

public abstract class BrushModeInputObject<T> : BrushModeInputObject
    where T : OngekiTimelineObjectBase
{
    public override Type ObjectType => typeof(T);

    public override OngekiTimelineObjectBase GenerateObject()
    {
        return Activator.CreateInstance<T>();
    }
}

public abstract class BrushModeInputLane<T> : BrushModeInputObject<T>
    where T : LaneStartBase, new()
{
    public override bool IsKeepExistingSelection => true;
}

public class BrushModeInputLaneLeft : BrushModeInputLane<LaneLeftStart>
{
    public override string DisplayName => "LaneLeft";
}

public class BrushModeInputLaneCenter : BrushModeInputLane<LaneCenterStart>
{
    public override string DisplayName => "LaneCenter";
}

public class BrushModeInputLaneRight : BrushModeInputLane<LaneRightStart>
{
    public override string DisplayName => "LaneRight";
}

public class BrushModeInputWallRight : BrushModeInputLane<WallRightStart>
{
    public override string DisplayName => "WallRight";
}

public class BrushModeInputWallLeft : BrushModeInputLane<WallLeftStart>
{
    public override string DisplayName => "WallLeft";
}

public class BrushModeInputLaneColorful : BrushModeInputLane<ColorfulLaneStart>
{
    public override string DisplayName => "LaneColorful";
}

public abstract class BrushModeInputHitObject<T> : BrushModeInputObject<T>
    where T : OngekiTimelineObjectBase, ICriticalableObject
{
    public override ObjectModificationAction ModifyObjectCtrl { get; } = new(CritObject, "Set critical");

    private static void CritObject(OngekiObjectBase baseObject)
    {
        ((ICriticalableObject)baseObject).IsCritical = true;
    }
}

public class BrushModeInputTap : BrushModeInputHitObject<Tap>
{
    public override string DisplayName => "Tap";
}

public class BrushModeInputHold : BrushModeInputHitObject<Hold>
{
    public override string DisplayName => "Hold";
    public override bool IsKeepExistingSelection => true;
}

public class BrushModeInputFlick : BrushModeInputHitObject<Flick>
{
    public override ObjectModificationAction ModifyObjectShift { get; } = new(SwitchFlick, "Switch direction");

    private static void SwitchFlick(OngekiObjectBase baseObject)
    {
        ((Flick)baseObject).Direction = Flick.FlickDirection.Right;
    }

    public override string DisplayName => "Flick";
}

public class BrushModeInputLaneBlock : BrushModeInputObject<LaneBlockArea>
{
    public override ObjectModificationAction ModifyObjectCtrl { get; } =
        new ObjectModificationAction(SwitchDirection, "Switch direction");

    private static void SwitchDirection(OngekiObjectBase baseObject)
    {
        ((LaneBlockArea)baseObject).Direction = LaneBlockArea.BlockDirection.Right;
    }

    public override string DisplayName => "LaneBlock";
}

public class BrushModeInputNormalBell : BrushModeInputObject<Bell>
{
    public override string DisplayName => "Bell";
}
