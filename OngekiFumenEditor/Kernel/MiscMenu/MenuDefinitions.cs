using Gemini.Framework.Menus;
using OngekiFumenEditor.Kernel.MiscMenu.Commands.About;
using OngekiFumenEditor.Kernel.MiscMenu.Commands.CallFullGC;
using OngekiFumenEditor.Kernel.MiscMenu.Commands.OpenUrlCommon;
using OngekiFumenEditor.Properties;
using System.ComponentModel.Composition;

namespace OngekiFumenEditor.Kernel.MiscMenu
{
	public static class MenuDefinitions
	{
		[Export]
		public static MenuItemGroupDefinition ProgramMiscOpenMenuGroup = new MenuItemGroupDefinition(Gemini.Modules.MainMenu.MenuDefinitions.FileMenu, 8);

		[Export]
		public static MenuItemDefinition ViewFumenMetaInfoBrowserMenuItem = new CommandMenuItemDefinition<CallFullGCCommandDefinition>(
			ProgramMiscOpenMenuGroup, 0);

		[Export]
		public static MenuDefinition HelpMenu = new MenuDefinition(Gemini.Modules.MainMenu.MenuDefinitions.MainMenuBar, 99999, Resources.MenuHelp);

		[Export]
		public static MenuItemGroupDefinition HelpMenuGroup = new MenuItemGroupDefinition(HelpMenu, 0);
		[Export]
		public static MenuItemGroupDefinition AboutMenuGroup = new MenuItemGroupDefinition(HelpMenu, 1);
		[Export]
		public static MenuItemDefinition OpenProjectUrlMenuItem = new CommandMenuItemDefinition<OpenProjectUrlCommandDefinition>(HelpMenuGroup, 0);
		[Export]
		public static MenuItemDefinition RequestIssueHelpMenuItem = new CommandMenuItemDefinition<RequestIssueHelpCommandDefinition>(HelpMenuGroup, 1);
		[Export]
		public static MenuItemDefinition PostSuggestUrlMenuItem = new CommandMenuItemDefinition<PostSuggestCommandDefinition>(HelpMenuGroup, 2);
		[Export]
		public static MenuItemDefinition UsageWikiMenuItem = new CommandMenuItemDefinition<UsageWikiCommandDefinition>(HelpMenuGroup, 2);
		[Export]
		public static MenuItemDefinition AboutMenuItem = new CommandMenuItemDefinition<AboutCommandDefinition>(AboutMenuGroup, 4);
	}
}