using Gemini.Framework.Menus;
using OngekiFumenEditor.Modules.TGridCalculatorToolViewer.Commands;
using System.ComponentModel.Composition;

namespace OngekiFumenEditor.Modules.TGridCalculatorToolViewer
{
	public static class MenuDefinitions
	{
		[Export]
		public static MenuItemDefinition ViewTGridCalculatorToolViewerMenuItem = new CommandMenuItemDefinition<ViewTGridCalculatorToolViewerCommandDefinition>(
			Gemini.Modules.MainMenu.MenuDefinitions.ToolsOptionsMenuGroup, 0);
	}
}