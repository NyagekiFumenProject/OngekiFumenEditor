using System.ComponentModel.Composition;
using Gemini.Framework.Menus;
using OngekiFumenEditor.Modules.FumenBulletPalleteListViewer.Commands;
using OngekiFumenEditor.Modules.FumenMetaInfoBrowser.Commands;
using OngekiFumenEditor.Modules.TGridCalculatorToolViewer.Commands;

namespace OngekiFumenEditor.Modules.TGridCalculatorToolViewer
{
    public static class MenuDefinitions
    {
        [Export]
        public static MenuItemDefinition ViewTGridCalculatorToolViewerMenuItem = new CommandMenuItemDefinition<ViewTGridCalculatorToolViewerCommandDefinition>(
            Gemini.Modules.MainMenu.MenuDefinitions.ToolsOptionsMenuGroup, 0);
    }
}