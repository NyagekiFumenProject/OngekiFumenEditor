using System.ComponentModel.Composition;
using Gemini.Framework.Menus;
using OngekiFumenEditor.Modules.FumenObjectPropertyBrowser.Commands;

namespace OngekiFumenEditor.Modules.FumenObjectPropertyBrowser
{
    public static class MenuDefinitions
    {
        [Export]
        public static MenuItemDefinition ViewFumenObjectPropertyBrowserMenuItem = new CommandMenuItemDefinition<ViewFumenObjectPropertyBrowserCommandDefinition>(
            Gemini.Modules.MainMenu.MenuDefinitions.ViewToolsMenuGroup, 2);
    }
}