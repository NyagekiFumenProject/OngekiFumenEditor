using Gekimini.Avalonia.Framework.Menus;
using OngekiFumenEditor.Avalonia.Modules.FumenObjectPropertyBrowser.Commands;
using Injectio.Attributes;

namespace OngekiFumenEditor.Avalonia.Modules.FumenObjectPropertyBrowser
{
	public static class MenuDefinitions
	{
		[RegisterStaticObject]
		public static MenuItemDefinition ViewFumenObjectPropertyBrowserMenuItem = new CommandMenuItemDefinition<ViewFumenObjectPropertyBrowserCommandDefinition>(
			Gekimini.Avalonia.Modules.MainMenu.MenuDefinitions.ViewToolsMenuGroup, 2);
	}
}


