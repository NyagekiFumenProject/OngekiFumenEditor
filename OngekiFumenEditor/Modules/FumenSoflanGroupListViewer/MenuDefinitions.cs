using Gemini.Framework.Menus;
using OngekiFumenEditor.Modules.FumenMetaInfoBrowser.Commands;
using OngekiFumenEditor.Modules.FumenSoflanGroupListViewer.Commands;
using System.ComponentModel.Composition;

namespace OngekiFumenEditor.Modules.FumenSoflanGroupListViewer
{
    public static class MenuDefinitions
    {
        [Export]
        public static MenuItemDefinition FumenSoflanGroupListViewerMenuItem = new CommandMenuItemDefinition<FumenSoflanGroupListViewerCommandDefinition>(
            Gemini.Modules.MainMenu.MenuDefinitions.ViewToolsMenuGroup, 2);
    }
}