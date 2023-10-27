using Gemini.Framework.Menus;
using OngekiFumenEditor.Modules.FumenVisualEditorSettings.Commands;
using System.ComponentModel.Composition;

namespace OngekiFumenEditor.Modules.FumenVisualEditorSettings
{
	public static class MenuDefinitions
	{
		[Export]
		public static MenuItemDefinition ViewFumenMetaInfoBrowserMenuItem = new CommandMenuItemDefinition<ViewFumenVisualEditorSettingsCommandDefinition>(
			Gemini.Modules.MainMenu.MenuDefinitions.ViewToolsMenuGroup, 2);
	}
}