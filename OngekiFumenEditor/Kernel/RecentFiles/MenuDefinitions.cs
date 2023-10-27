using Gemini.Framework.Menus;
using OngekiFumenEditor.Kernel.RecentFiles.Commands;
using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Primitives;

namespace OngekiFumenEditor.Kernel.RecentFiles
{
	public static class MenuDefinitions
	{
		[Export]
		public static MenuItemGroupDefinition FileOpenRecentMenuGroup = new MenuItemGroupDefinition(Gemini.Modules.MainMenu.MenuDefinitions.FileMenu, 9);

		[Export]
		public static MenuItemDefinition FileRecentFilesMenuItem = new CommandMenuItemDefinition<RecentFilesCommandDefinition>(
			FileOpenRecentMenuGroup, 0);

		[Export]
		public static MenuItemGroupDefinition FileRecentFilesCascadeGroup = new MenuItemGroupDefinition(
			FileRecentFilesMenuItem, 0);

		[Export]
		public static MenuItemDefinition FileOpenRecentMenuItemList = new CommandMenuItemDefinition<OpenRecentFileCommandListDefinition>(
			FileRecentFilesCascadeGroup, 0);
	}
}