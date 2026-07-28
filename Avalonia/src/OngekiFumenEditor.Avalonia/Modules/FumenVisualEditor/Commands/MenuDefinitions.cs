using Gekimini.Avalonia.Framework.Menus;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Commands.ClearHistory;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Commands.RecalculateTotalHeight;
using Injectio.Attributes;

namespace OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Commands
{
	public static class MenuDefinitions
	{
		[RegisterStaticObject]
		public static MenuItemDefinition ClearHistoryMenuItem = new CommandMenuItemDefinition<ClearHistoryCommandDefinition>(
			Gekimini.Avalonia.Modules.MainMenu.MenuDefinitions.EditUndoRedoMenuGroup, 2);

        [RegisterStaticObject]
        public static MenuItemDefinition RecalculateTotalHeightMenuItem = new CommandMenuItemDefinition<RecalculateTotalHeightCommandDefinition>(
            Gekimini.Avalonia.Modules.MainMenu.MenuDefinitions.ToolsOptionsMenuGroup, 2);
    }
}



