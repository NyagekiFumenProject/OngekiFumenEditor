using Gemini.Framework.Commands;
using Gemini.Framework.Menus;
using Gemini.Framework.ToolBars;
using Gemini.Modules.UndoRedo.Commands;
using OngekiFumenEditor.Modules.FumenVisualEditor.Commands.BrushModeSwitch;
using OngekiFumenEditor.Modules.FumenVisualEditor.Commands.ClearHistory;
using OngekiFumenEditor.Modules.FumenVisualEditor.Commands.EditorModeSwitch;
using OngekiFumenEditor.Modules.FumenVisualEditor.Commands.ShowCurveControlAlways;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Commands
{
    public static class MenuDefinitions
    {
        [Export]
        public static MenuItemDefinition ClearHistoryMenuItem = new CommandMenuItemDefinition<ClearHistoryCommandDefinition>(
            Gemini.Modules.MainMenu.MenuDefinitions.EditUndoRedoMenuGroup, 2);
    }
}
