using Gemini.Framework.Menus;
using OngekiFumenEditor.Kernel.AssistHelper.Commands.AdjustDockablesHorizonPosition;
using OngekiFumenEditor.Properties;
using System.ComponentModel.Composition;

namespace OngekiFumenEditor.Kernel.AssistHelper.Commands
{
	public static class MenuDefinitions
	{
		[Export]
		public static MenuDefinition AssistMenu = new MenuDefinition(Gemini.Modules.MainMenu.MenuDefinitions.MainMenuBar, 7, Resources.MenuAssist);
		[Export]
		public static MenuItemGroupDefinition AssistMenuGroup = new MenuItemGroupDefinition(AssistMenu, 0);
		[Export]
		public static MenuItemDefinition ViewFumenMetaInfoBrowserMenuItem = new CommandMenuItemDefinition<AdjustDockablesHorizonPositionCommandDefinition>(
			AssistMenuGroup, 0);
	}
}
