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

    [Export] public static ToolBarItemDefinition BatchModeWallLeftItemDefinition = new CommandToolBarItemDefinition<BatchModeWallLeftCommandDefinition>(BatchModeToolBarGroup, 0);
    [Export] public static ToolBarItemDefinition BatchModeLaneLeftItemDefinition = new CommandToolBarItemDefinition<BatchModeLaneLeftCommandDefinition>( BatchModeToolBarGroup, 1);
    [Export] public static ToolBarItemDefinition BatchModeLaneCenterItemDefinition = new CommandToolBarItemDefinition<BatchModeLaneCenterCommandDefinition>(BatchModeToolBarGroup, 2);
    [Export] public static ToolBarItemDefinition BatchModeLaneRightItemDefinition = new CommandToolBarItemDefinition<BatchModeLaneRightCommandDefinition>(BatchModeToolBarGroup, 3);
    [Export] public static ToolBarItemDefinition BatchModeWallRightItemDefinition = new CommandToolBarItemDefinition<BatchModeWallRightCommandDefinition>(BatchModeToolBarGroup, 4);
    [Export] public static ToolBarItemDefinition BatchModeLaneColorfulItemDefinition = new CommandToolBarItemDefinition<BatchModeLaneColorfulCommandDefinition>(BatchModeToolBarGroup, 5);
    [Export] public static ToolBarItemDefinition BatchModeInputTapItemDefinition = new CommandToolBarItemDefinition<BatchModeTapCommandDefinition>( BatchModeToolBarGroup, 6);
    [Export] public static ToolBarItemDefinition BatchModeHoldItemDefinition = new CommandToolBarItemDefinition<BatchModeHoldCommandDefinition>(BatchModeToolBarGroup, 7);
    [Export] public static ToolBarItemDefinition BatchModeFlickItemDefinition = new CommandToolBarItemDefinition<BatchModeFlickCommandDefinition>(BatchModeToolBarGroup, 8);
    [Export] public static ToolBarItemDefinition BatchModeBellItemDefinition = new CommandToolBarItemDefinition<BatchModeBellCommandDefinition>(BatchModeToolBarGroup, 9);
    [Export] public static ToolBarItemDefinition BatchModeLaneBlockItemDefinition = new CommandToolBarItemDefinition<BatchModeLaneBlockCommandDefinition>(BatchModeToolBarGroup, 10);
    [Export] public static ToolBarItemDefinition BatchModeClipboardItemDefinition = new CommandToolBarItemDefinition<BatchModeClipboardCommandDefinition>(BatchModeToolBarGroup, 11);
    [Export] public static ToolBarItemDefinition BatchModeFilterLanesItemDefinition = new CommandToolBarItemDefinition<BatchModeFilterLanesCommandDefinition>(BatchModeToolBarGroup, 12);
    [Export] public static ToolBarItemDefinition BatchModeFilterDockableItemDefinition = new CommandToolBarItemDefinition<BatchModeFilterDockableObjectsCommandDefinition>(BatchModeToolBarGroup, 13);
    [Export] public static ToolBarItemDefinition BatchModeFilterFloatingItemDefinition = new CommandToolBarItemDefinition<BatchModeFilterFloatingObjectsCommandDefinition>(BatchModeToolBarGroup, 14);
}