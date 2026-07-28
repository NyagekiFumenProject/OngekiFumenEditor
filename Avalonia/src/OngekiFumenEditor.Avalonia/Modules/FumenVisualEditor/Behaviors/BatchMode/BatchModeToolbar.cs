using Gekimini.Avalonia.Framework.ToolBars;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Commands;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Commands.BatchModeToggle;
using Injectio.Attributes;

namespace OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Behaviors.BatchMode;

public class BatchModeToolbar
{
    [RegisterStaticObject]
    public static ToolBarDefinition BatchModeToolbarDefinition = new ToolBarDefinition(9, "Batch Mode");

    [RegisterStaticObject]
    public static ToolBarItemGroupDefinition BatchModeToolBarGroup = new ToolBarItemGroupDefinition(BatchModeToolbarDefinition, 0);

    [RegisterStaticObject] public static ToolBarItemDefinition BatchModeWallLeftItemDefinition = new CommandToolBarItemDefinition<BatchModeInputWallLeft>(BatchModeToolBarGroup, 0);
    [RegisterStaticObject] public static ToolBarItemDefinition BatchModeLaneLeftItemDefinition = new CommandToolBarItemDefinition<BatchModeInputLaneLeft>(BatchModeToolBarGroup, 1);
    [RegisterStaticObject] public static ToolBarItemDefinition BatchModeLaneCenterItemDefinition = new CommandToolBarItemDefinition<BatchModeInputLaneCenter>(BatchModeToolBarGroup, 2);
    [RegisterStaticObject] public static ToolBarItemDefinition BatchModeLaneRightItemDefinition = new CommandToolBarItemDefinition<BatchModeInputLaneRight>(BatchModeToolBarGroup, 3);
    [RegisterStaticObject] public static ToolBarItemDefinition BatchModeWallRightItemDefinition = new CommandToolBarItemDefinition<BatchModeInputWallRight>(BatchModeToolBarGroup, 4);
    [RegisterStaticObject] public static ToolBarItemDefinition BatchModeLaneColorfulItemDefinition = new CommandToolBarItemDefinition<BatchModeInputLaneColorful>(BatchModeToolBarGroup, 5);
    [RegisterStaticObject] public static ToolBarItemDefinition BatchModeInputTapItemDefinition = new CommandToolBarItemDefinition<BatchModeInputTap>( BatchModeToolBarGroup, 6);
    [RegisterStaticObject] public static ToolBarItemDefinition BatchModeHoldItemDefinition = new CommandToolBarItemDefinition<BatchModeInputHold>(BatchModeToolBarGroup, 7);
    [RegisterStaticObject] public static ToolBarItemDefinition BatchModeFlickItemDefinition = new CommandToolBarItemDefinition<BatchModeInputFlick>(BatchModeToolBarGroup, 8);
    [RegisterStaticObject] public static ToolBarItemDefinition BatchModeBellItemDefinition = new CommandToolBarItemDefinition<BatchModeInputNormalBell>(BatchModeToolBarGroup, 9);
    [RegisterStaticObject] public static ToolBarItemDefinition BatchModeLaneBlockItemDefinition = new CommandToolBarItemDefinition<BatchModeInputLaneBlock>(BatchModeToolBarGroup, 10);
    [RegisterStaticObject] public static ToolBarItemDefinition BatchModeClipboardItemDefinition = new CommandToolBarItemDefinition<BatchModeInputClipboard>(BatchModeToolBarGroup, 11);
    [RegisterStaticObject] public static ToolBarItemDefinition BatchModeFilterLanesItemDefinition = new CommandToolBarItemDefinition<BatchModeFilterLanes>(BatchModeToolBarGroup, 100);
    [RegisterStaticObject] public static ToolBarItemDefinition BatchModeFilterDockableItemDefinition = new CommandToolBarItemDefinition<BatchModeFilterDockableObjects>(BatchModeToolBarGroup, 101);
    [RegisterStaticObject] public static ToolBarItemDefinition BatchModeFilterFloatingItemDefinition = new CommandToolBarItemDefinition<BatchModeFilterFloatingObjects>(BatchModeToolBarGroup, 102);
}


