using Gemini.Framework.Menus;
using OngekiFumenEditor.Modules.FumenBulletPalleteListViewer.Commands;
using System.ComponentModel.Composition;

namespace OngekiFumenEditor.Modules.FumenBulletPalleteListViewer
{
	public static class MenuDefinitions
	{
		[Export]
		public static MenuItemDefinition ViewFumenMetaInfoBrowserMenuItem = new CommandMenuItemDefinition<ViewFumenBulletPalleteListViewerCommandDefinition>(
			Gemini.Modules.MainMenu.MenuDefinitions.ViewToolsMenuGroup, 2);
	}
}