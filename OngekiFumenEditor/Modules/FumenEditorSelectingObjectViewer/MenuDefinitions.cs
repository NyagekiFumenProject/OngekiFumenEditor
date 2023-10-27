using Gemini.Framework.Menus;
using OngekiFumenEditor.Modules.FumenEditorSelectingObjectViewer.Commands;
using System.ComponentModel.Composition;

namespace OngekiFumenEditor.Modules.IFumenEditorSelectingObjectViewer
{
	public static class MenuDefinitions
	{
		[Export]
		public static MenuItemDefinition ViewFumenEditorSelectingObjectViewerMenuItem = new CommandMenuItemDefinition<ViewFumenEditorSelectingObjectViewerCommandDefinition>(
			Gemini.Modules.MainMenu.MenuDefinitions.ViewToolsMenuGroup, 2);
	}
}