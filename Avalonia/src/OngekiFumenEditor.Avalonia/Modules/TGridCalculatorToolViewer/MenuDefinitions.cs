using Gekimini.Avalonia.Framework.Menus;
using OngekiFumenEditor.Avalonia.Modules.TGridCalculatorToolViewer.Commands;
using Injectio.Attributes;

namespace OngekiFumenEditor.Avalonia.Modules.TGridCalculatorToolViewer
{
	public static class MenuDefinitions
	{
		[RegisterStaticObject]
		public static MenuItemDefinition ViewTGridCalculatorToolViewerMenuItem = new CommandMenuItemDefinition<ViewTGridCalculatorToolViewerCommandDefinition>(
			Gekimini.Avalonia.Modules.MainMenu.MenuDefinitions.ToolsOptionsMenuGroup, 0);
	}
}


