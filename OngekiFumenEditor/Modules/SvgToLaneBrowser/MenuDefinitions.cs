using System.ComponentModel.Composition;
using Gemini.Framework.Menus;
using OngekiFumenEditor.Modules.SvgToLaneBrowser.Commands;

namespace OngekiFumenEditor.Modules.SvgToLaneBrowser
{
    public static class MenuDefinitions
    {
        [Export]
        public static MenuItemDefinition ViewSvgToLaneBrowserViewerMenuItem = new CommandMenuItemDefinition<ViewSvgToLaneBrowserCommandDefinition>(
            Gemini.Modules.MainMenu.MenuDefinitions.ToolsOptionsMenuGroup, 0);
    }
}