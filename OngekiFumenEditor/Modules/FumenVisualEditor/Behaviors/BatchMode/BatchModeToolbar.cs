using System.ComponentModel.Composition;
using Gemini.Framework.ToolBars;
using Microsoft.CodeAnalysis.CodeRefactorings;
using OngekiFumenEditor.Modules.FumenVisualEditor.Commands;
using OngekiFumenEditor.Modules.FumenVisualEditor.Commands.BatchModeToggle;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Behaviors.BatchMode;

public class BatchModeToolbar
{
    [Export]
    public static ToolBarDefinition BatchModeToolbarDefinition = new ToolBarDefinition(9, "Batch Mode");

    [Export]
    public static ToolBarItemGroupDefinition BatchModeToolBarGroup = new ToolBarItemGroupDefinition(BatchModeToolbarDefinition, 0);

    [Export] public static ToolBarItemDefinition BatchModeWallLeftItemDefinition = new CommandToolBarItemDefinition<BatchModeInputWallLeft>(BatchModeToolBarGroup, 0);
    [Export] public static ToolBarItemDefinition BatchModeLaneLeftItemDefinition = new CommandToolBarItemDefinition<BatchModeInputLaneLeft>(BatchModeToolBarGroup, 1);
    [Export] public static ToolBarItemDefinition BatchModeLaneCenterItemDefinition = new CommandToolBarItemDefinition<BatchModeInputLaneCenter>(BatchModeToolBarGroup, 2);
    [Export] public static ToolBarItemDefinition BatchModeLaneRightItemDefinition = new CommandToolBarItemDefinition<BatchModeInputLaneRight>(BatchModeToolBarGroup, 3);
    [Export] public static ToolBarItemDefinition BatchModeWallRightItemDefinition = new CommandToolBarItemDefinition<BatchModeInputWallRight>(BatchModeToolBarGroup, 4);
    [Export] public static ToolBarItemDefinition BatchModeLaneColorfulItemDefinition = new CommandToolBarItemDefinition<BatchModeInputLaneColorful>(BatchModeToolBarGroup, 5);
    [Export] public static ToolBarItemDefinition BatchModeInputTapItemDefinition = new CommandToolBarItemDefinition<BatchModeInputTap>( BatchModeToolBarGroup, 6);
    [Export] public static ToolBarItemDefinition BatchModeHoldItemDefinition = new CommandToolBarItemDefinition<BatchModeInputHold>(BatchModeToolBarGroup, 7);
    [Export] public static ToolBarItemDefinition BatchModeFlickItemDefinition = new CommandToolBarItemDefinition<BatchModeInputFlick>(BatchModeToolBarGroup, 8);
    [Export] public static ToolBarItemDefinition BatchModeBellItemDefinition = new CommandToolBarItemDefinition<BatchModeInputNormalBell>(BatchModeToolBarGroup, 9);
    [Export] public static ToolBarItemDefinition BatchModeLaneBlockItemDefinition = new CommandToolBarItemDefinition<BatchModeInputLaneBlock>(BatchModeToolBarGroup, 10);
    [Export] public static ToolBarItemDefinition BatchModeClipboardItemDefinition = new CommandToolBarItemDefinition<BatchModeInputClipboard>(BatchModeToolBarGroup, 11);
    [Export] public static ToolBarItemDefinition BatchModeFilterLanesItemDefinition = new CommandToolBarItemDefinition<BatchModeFilterLanes>(BatchModeToolBarGroup, 100);
    [Export] public static ToolBarItemDefinition BatchModeFilterDockableItemDefinition = new CommandToolBarItemDefinition<BatchModeFilterDockableObjects>(BatchModeToolBarGroup, 101);
    [Export] public static ToolBarItemDefinition BatchModeFilterFloatingItemDefinition = new CommandToolBarItemDefinition<BatchModeFilterFloatingObjects>(BatchModeToolBarGroup, 102);
}