using System.ComponentModel.Composition;
using Gemini.Framework.Menus;
using OngekiFumenEditor.Modules.FumenMetaInfoBrowser.Commands;
using OngekiFumenEditor.Modules.FumenTimeSignatureListViewer.Commands;

namespace OngekiFumenEditor.Modules.FumenTimeSignatureListViewer
{
    public static class MenuDefinitions
    {
        [Export]
        public static MenuItemDefinition ViewFumenTimeSignatureListViewerMenuItem = new CommandMenuItemDefinition<ViewFumenTimeSignatureListViewerCommandDefinition>(
            Gemini.Modules.MainMenu.MenuDefinitions.ViewToolsMenuGroup, 2);
    }
}