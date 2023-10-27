using Gemini.Framework.Menus;
using OngekiFumenEditor.Modules.FumenVisualEditor.Commands.ClearHistory;
using System.ComponentModel.Composition;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Commands
{
	public static class MenuDefinitions
	{
		[Export]
		public static MenuItemDefinition ClearHistoryMenuItem = new CommandMenuItemDefinition<ClearHistoryCommandDefinition>(
			Gemini.Modules.MainMenu.MenuDefinitions.EditUndoRedoMenuGroup, 2);
	}
}
