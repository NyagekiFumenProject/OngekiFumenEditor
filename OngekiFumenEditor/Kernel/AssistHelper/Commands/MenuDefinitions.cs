using Gemini.Framework.Menus;
using OngekiFumenEditor.Kernel.AssistHelper.Commands.AdjustDockablesHorizonPosition;
using OngekiFumenEditor.Kernel.AssistHelper.Commands.GenerateAutoplayFaderData;
using System.ComponentModel.Composition;

namespace OngekiFumenEditor.Kernel.AssistHelper.Commands
{
	public static class MenuDefinitions
	{
		[Export]
		public static MenuDefinition AssistMenu = new MenuDefinition(Gemini.Modules.MainMenu.MenuDefinitions.MainMenuBar, 7, "辅助 (_A)");
		[Export]
		public static MenuItemGroupDefinition AssistMenuGroup = new MenuItemGroupDefinition(AssistMenu, 0);
		[Export]
		public static MenuItemDefinition ViewFumenMetaInfoBrowserMenuItem = new CommandMenuItemDefinition<AdjustDockablesHorizonPositionCommandDefinition>(
			AssistMenuGroup, 0); 
		[Export]
		public static MenuItemDefinition GenerateAutoplayFaderDataMenuItem = new CommandMenuItemDefinition<GenerateAutoplayFaderDataCommandDefinition>(
			AssistMenuGroup, 1);
	}
}
