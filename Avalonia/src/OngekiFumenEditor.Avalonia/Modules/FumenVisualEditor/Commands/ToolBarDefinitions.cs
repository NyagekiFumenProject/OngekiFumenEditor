using Gekimini.Avalonia.Framework.ToolBars;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Commands.EditorModeSwitch;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Commands.ShowCurveControlAlways;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Commands.BatchModeToggle;
using Injectio.Attributes;

namespace OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Commands
{
	public static class ToolBarDefinitions
	{
		[RegisterStaticObject]
		public static ToolBarDefinition EditorToolBar = new ToolBarDefinition(8, "Editor");

		[RegisterStaticObject]
		public static ToolBarItemGroupDefinition EditorStatusToolBarGroup = new ToolBarItemGroupDefinition(EditorToolBar, 0);

		[RegisterStaticObject]
		public static ToolBarItemDefinition BatchModeSwitchToolBarItem = new CommandToolBarItemDefinition<BatchModeToggleCommandDefinition>(
			EditorStatusToolBarGroup, 0);

		[RegisterStaticObject]
		public static ToolBarItemDefinition ShowCurveControlAlwaysToolBarItem = new CommandToolBarItemDefinition<ShowCurveControlAlwaysCommandDefinition>(
			EditorStatusToolBarGroup, 1);

		[RegisterStaticObject]
		public static ToolBarItemDefinition EditorModeSwitchToolBarItem = new CommandToolBarItemDefinition<EditorModeSwitchCommandDefinition>(
			EditorStatusToolBarGroup, 2);
	}
}



