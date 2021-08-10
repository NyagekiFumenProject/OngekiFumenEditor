using Gemini.Framework.Menus;
using Gemini.Framework.ToolBars;
using OngekiFumenEditor.Modules.TextEditor.Commands;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Modules.TextEditor
{
    public static class MenuDefinitions
    {
        [Export]
        public static MenuItemDefinition FileSaveAsMenuItem = new CommandMenuItemDefinition<ReloadTextEditorViewerCommandDefinition>(
               Gemini.Modules.MainMenu.MenuDefinitions.FileCloseMenuGroup, 1);
    }
}
