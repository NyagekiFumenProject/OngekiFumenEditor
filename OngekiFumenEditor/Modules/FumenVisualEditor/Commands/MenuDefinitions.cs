using Gemini.Framework.Menus;
using OngekiFumenEditor.Modules.FumenVisualEditor.Commands.ClearHistory;
using OngekiFumenEditor.Modules.FumenVisualEditor.Commands.RecalculateTotalHeight;
using System.ComponentModel.Composition;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Commands
{
	public static class MenuDefinitions
	{
		[Export]
		public static MenuItemDefinition ClearHistoryMenuItem = new CommandMenuItemDefinition<ClearHistoryCommandDefinition>(
			Gemini.Modules.MainMenu.MenuDefinitions.EditUndoRedoMenuGroup, 2);

        [Export]
        public static MenuItemDefinition RecalculateTotalHeightMenuItem = new CommandMenuItemDefinition<RecalculateTotalHeightCommandDefinition>(
            Gemini.Modules.MainMenu.MenuDefinitions.ToolsOptionsMenuGroup, 2);
    }
}
