using Gemini.Framework.Menus;
using OngekiFumenEditor.Modules.OptionGeneratorTools.Commands;
using System.ComponentModel.Composition;

namespace OngekiFumenEditor.Modules.OptionGeneratorTools
{
	public static class MenuDefintions
	{
		[Export]
		public static MenuItemDefinition ToolsOptionsMenuGroupMenuItem = new TextMenuItemDefinition(
			Gemini.Modules.MainMenu.MenuDefinitions.ToolsOptionsMenuGroup, 0, "option生成工具");

		[Export]
		public static MenuItemGroupDefinition OptionGeneratorToolsMenuGroup = new MenuItemGroupDefinition(ToolsOptionsMenuGroupMenuItem, 100);

		[Export]
		public static MenuItemDefinition ViewMusicXmlWindowMenuItem = new CommandMenuItemDefinition<ViewMusicXmlWindowCommandDefinition>(
			OptionGeneratorToolsMenuGroup, 0);

		[Export]
		public static MenuItemDefinition ViewJacketGeneratorWindowMenuItem = new CommandMenuItemDefinition<ViewJacketGeneratorWindowCommandDefinition>(
			OptionGeneratorToolsMenuGroup, 0);

		[Export]
		public static MenuItemDefinition ViewAcbGeneratorWindowMenuItem = new CommandMenuItemDefinition<ViewAcbGeneratorWindowCommandDefinition>(
			OptionGeneratorToolsMenuGroup, 0);

	}
}