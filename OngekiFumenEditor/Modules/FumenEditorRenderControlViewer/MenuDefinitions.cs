using Gemini.Framework.Menus;
using OngekiFumenEditor.Modules.FumenEditorRenderControlViewer.Commands;
using System.ComponentModel.Composition;

namespace OngekiFumenEditor.Modules.FumenEditorRenderControlViewer
{
	public static class MenuDefinitions
	{
		[Export]
		public static MenuItemDefinition FumenEditorRenderControlViewerMenuItem = new CommandMenuItemDefinition<FumenEditorRenderControlViewerCommandDefinition>(
			Gemini.Modules.MainMenu.MenuDefinitions.ViewToolsMenuGroup, 2);
	}
}