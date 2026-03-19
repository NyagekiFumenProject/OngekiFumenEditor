using Gemini.Framework.Menus;
using OngekiFumenEditor.Modules.OngekiGamePlayControllerViewer.Commands;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Modules.OngekiGamePlayControllerViewer
{
    public static class MenuDefinitions
    {
        [Export]
        public static MenuItemDefinition OngekiGamePlayControllerViewerMenuItem = new CommandMenuItemDefinition<OngekiGamePlayControllerViewerCommandDefinition>(
            Gemini.Modules.MainMenu.MenuDefinitions.ToolsOptionsMenuGroup, 0);
    }
}
