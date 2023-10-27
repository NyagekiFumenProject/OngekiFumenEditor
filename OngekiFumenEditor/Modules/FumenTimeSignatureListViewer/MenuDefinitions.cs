using Gemini.Framework.Menus;
using OngekiFumenEditor.Modules.FumenTimeSignatureListViewer.Commands;
using System.ComponentModel.Composition;

namespace OngekiFumenEditor.Modules.FumenTimeSignatureListViewer
{
	public static class MenuDefinitions
	{
		[Export]
		public static MenuItemDefinition ViewFumenTimeSignatureListViewerMenuItem = new CommandMenuItemDefinition<ViewFumenTimeSignatureListViewerCommandDefinition>(
			Gemini.Modules.MainMenu.MenuDefinitions.ViewToolsMenuGroup, 2);
	}
}