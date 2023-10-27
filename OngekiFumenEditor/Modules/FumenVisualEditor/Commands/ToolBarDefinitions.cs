using Gemini.Framework.ToolBars;
using OngekiFumenEditor.Modules.FumenVisualEditor.Commands.BrushModeSwitch;
using OngekiFumenEditor.Modules.FumenVisualEditor.Commands.EditorModeSwitch;
using OngekiFumenEditor.Modules.FumenVisualEditor.Commands.ShowCurveControlAlways;
using System.ComponentModel.Composition;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Commands
{
	public static class ToolBarDefinitions
	{
		[Export]
		public static ToolBarDefinition EditorToolBar = new ToolBarDefinition(8, "Editor");

		[Export]
		public static ToolBarItemGroupDefinition EditorStatusToolBarGroup = new ToolBarItemGroupDefinition(EditorToolBar, 0);

		[Export]
		public static ToolBarItemDefinition BrushModeSwitchToolBarItem = new CommandToolBarItemDefinition<BrushModeSwitchCommandDefinition>(
			EditorStatusToolBarGroup, 0);

		[Export]
		public static ToolBarItemDefinition ShowCurveControlAlwaysToolBarItem = new CommandToolBarItemDefinition<ShowCurveControlAlwaysCommandDefinition>(
			EditorStatusToolBarGroup, 1);

		[Export]
		public static ToolBarItemDefinition EditorModeSwitchToolBarItem = new CommandToolBarItemDefinition<EditorModeSwitchCommandDefinition>(
			EditorStatusToolBarGroup, 2);
	}
}
