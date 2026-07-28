using Gekimini.Avalonia.Framework.Menus;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditorSettings.Commands;
using Injectio.Attributes;

namespace OngekiFumenEditor.Avalonia.Modules.FumenVisualEditorSettings
{
	public static class MenuDefinitions
	{
		[RegisterStaticObject]
		public static MenuItemDefinition ViewFumenMetaInfoBrowserMenuItem = new CommandMenuItemDefinition<ViewFumenVisualEditorSettingsCommandDefinition>(
			Gekimini.Avalonia.Modules.MainMenu.MenuDefinitions.ViewToolsMenuGroup, 2);
	}
}


