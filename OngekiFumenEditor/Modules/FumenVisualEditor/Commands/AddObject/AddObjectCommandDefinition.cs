using Gemini.Framework.Commands;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects.Lane;
using OngekiFumenEditor.Base.OngekiObjects.Wall;
using OngekiFumenEditor.Utils;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Commands.AddObject;

[CommandDefinition]
public abstract class AddObjectCommandDefinition : CommandDefinition
{
    public abstract OngekiMovableObjectBase CreateOngekiObject();
    public abstract string LaneName { get; }

    public override string Name => $"AddObject_{LaneName}";
    public override string Text => $"Add {LaneName}";
    public override string ToolTip => $"Add a new {LaneName} to the fumen and select it";
}

[CommandDefinition]
public class AddObjectLaneLeftCommandDefinition : AddObjectCommandDefinition
{
    public override OngekiMovableObjectBase CreateOngekiObject() => new LaneLeftStart();
    public override string LaneName => "LaneLeft";
}

[CommandDefinition]
public class AddObjectLaneCenterCommandDefinition : AddObjectCommandDefinition
{
    public override OngekiMovableObjectBase CreateOngekiObject() => new LaneCenterStart();
    public override string LaneName => "LaneCenter";
}

[CommandDefinition]
public class AddObjectLaneRightCommandDefinition : AddObjectCommandDefinition
{
    public override OngekiMovableObjectBase CreateOngekiObject() => new LaneRightStart();
    public override string LaneName => "LaneRight";
}

[CommandDefinition]
public class AddObjectWallRightCommandDefinition : AddObjectCommandDefinition
{
    public override OngekiMovableObjectBase CreateOngekiObject() => new WallRightStart();
    public override string LaneName => "WallRight";
}

[CommandDefinition]
public class AddObjectWallLeftCommandDefinition : AddObjectCommandDefinition
{
    public override OngekiMovableObjectBase CreateOngekiObject() => new WallLeftStart();
    public override string LaneName => "WallLeft";
}
