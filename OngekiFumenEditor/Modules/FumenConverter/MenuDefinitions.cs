using Gemini.Framework.Menus;
using OngekiFumenEditor.Modules.FumenConverter.Commands;
using System.ComponentModel.Composition;

namespace OngekiFumenEditor.Modules.SvgToLaneBrowser
{
	public static class FumenConverter
	{
		[Export]
		public static MenuItemDefinition ViewFumenConverterMenuItem = new CommandMenuItemDefinition<ViewFumenConverterCommandDefinition>(
			Gemini.Modules.MainMenu.MenuDefinitions.ToolsOptionsMenuGroup, 0);
	}
}