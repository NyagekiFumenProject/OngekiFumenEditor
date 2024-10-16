using Gemini.Framework.Commands;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Base.OngekiObjects.Lane;
using OngekiFumenEditor.Base.OngekiObjects.Wall;
using OngekiFumenEditor.Utils;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Commands.AddObject;

public abstract class AddObjectCommandDefinition : CommandDefinition
{
    public abstract OngekiMovableObjectBase CreateOngekiObject();
    public abstract string ObjectName { get; }
    public virtual bool AddToSelection { get; } = false;

    public override string Name => $"AddObject_{ObjectName}";
    public override string Text => $"Add {ObjectName}";
    public override string ToolTip => 
        AddToSelection ? $"Add a new {ObjectName} to the fumen and select it" : $"Add a new {ObjectName} to the fumen";
}

[CommandDefinition]
public abstract class AddLaneCommandDefinition : AddObjectCommandDefinition
{
    public override bool AddToSelection { get; } = true;
}

[CommandDefinition]
public class AddLaneLeftCommandDefinition : AddLaneCommandDefinition
{
    public override OngekiMovableObjectBase CreateOngekiObject() => new LaneLeftStart();
    public override string ObjectName => "LaneLeft";
}

[CommandDefinition]
public class AddLaneCenterCommandDefinition : AddLaneCommandDefinition
{
    public override OngekiMovableObjectBase CreateOngekiObject() => new LaneCenterStart();
    public override string ObjectName => "LaneCenter";
}

[CommandDefinition]
public class AddLaneRightCommandDefinition : AddLaneCommandDefinition
{
    public override OngekiMovableObjectBase CreateOngekiObject() => new LaneRightStart();
    public override string ObjectName => "LaneRight";
}

[CommandDefinition]
public class AddWallRightCommandDefinition : AddLaneCommandDefinition
{
    public override OngekiMovableObjectBase CreateOngekiObject() => new WallRightStart();
    public override string ObjectName => "WallRight";
}

[CommandDefinition]
public class AddWallLeftCommandDefinition : AddLaneCommandDefinition
{
    public override OngekiMovableObjectBase CreateOngekiObject() => new WallLeftStart();
    public override string ObjectName => "WallLeft";
}

[CommandDefinition]
public class AddLaneColorfulCommandDefinition : AddLaneCommandDefinition
{
    public override OngekiMovableObjectBase CreateOngekiObject() => new ColorfulLaneStart();
    public override string ObjectName => "LaneColorful";
}

[CommandDefinition]
public class AddTapCommandDefinition : AddObjectCommandDefinition
{
    public override OngekiMovableObjectBase CreateOngekiObject() => new Tap();
    public override string ObjectName => "Tap";
}

[CommandDefinition]
public class AddHoldCommandDefinition : AddObjectCommandDefinition
{
    public override OngekiMovableObjectBase CreateOngekiObject() => new Hold();
    public override string ObjectName => "Hold";
    public override bool AddToSelection => true;
}

[CommandDefinition]
public class AddFlickCommandDefinition : AddObjectCommandDefinition
{
    public override OngekiMovableObjectBase CreateOngekiObject() => new Flick();
    public override string ObjectName => "Flick";
}

[CommandDefinition]
public class AddFlickReversedCommandDefinition : AddFlickCommandDefinition
{
    public override OngekiMovableObjectBase CreateOngekiObject() => new Flick() { Direction = Flick.FlickDirection.Right };
}

[CommandDefinition]
public class AddLaneBlockCommandDefinition : AddObjectCommandDefinition
{
    public override OngekiMovableObjectBase CreateOngekiObject() => new Flick();
    public override string ObjectName => "LaneBlock";
}
