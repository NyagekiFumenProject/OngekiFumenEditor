using Gemini.Framework.Menus;
using OngekiFumenEditor.Modules.FumenCheckerListViewer.Commands;
using System.ComponentModel.Composition;

namespace OngekiFumenEditor.Modules.FumenCheckerListViewer
{
	public static class MenuDefinitions
	{
		[Export]
		public static MenuItemDefinition ViewFumenCheckerListViewerMenuItem = new CommandMenuItemDefinition<ViewFumenCheckerListViewerCommandDefinition>(
			Gemini.Modules.MainMenu.MenuDefinitions.ViewToolsMenuGroup, 2);
	}
}