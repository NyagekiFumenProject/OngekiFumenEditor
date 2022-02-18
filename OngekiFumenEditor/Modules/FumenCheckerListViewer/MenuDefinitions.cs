using System.ComponentModel.Composition;
using Gemini.Framework.Menus;
using OngekiFumenEditor.Modules.FumenBulletPalleteListViewer.Commands;
using OngekiFumenEditor.Modules.FumenCheckerListViewer.Commands;
using OngekiFumenEditor.Modules.FumenMetaInfoBrowser.Commands;

namespace OngekiFumenEditor.Modules.FumenCheckerListViewer
{
    public static class MenuDefinitions
    {
        [Export]
        public static MenuItemDefinition ViewFumenCheckerListViewerMenuItem = new CommandMenuItemDefinition<ViewFumenCheckerListViewerCommandDefinition>(
            Gemini.Modules.MainMenu.MenuDefinitions.ViewToolsMenuGroup, 2);
    }
}