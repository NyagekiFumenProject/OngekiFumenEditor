using Gemini.Framework.Menus;
using OngekiFumenEditor.Modules.FumenMetaInfoBrowser.Commands;
using System.ComponentModel.Composition;

namespace OngekiFumenEditor.Modules.FumenMetaInfoBrowser
{
	public static class MenuDefinitions
	{
		[Export]
		public static MenuItemDefinition ViewFumenMetaInfoBrowserMenuItem = new CommandMenuItemDefinition<ViewFumenMetaInfoBrowserCommandDefinition>(
			Gemini.Modules.MainMenu.MenuDefinitions.ViewToolsMenuGroup, 2);
	}
}