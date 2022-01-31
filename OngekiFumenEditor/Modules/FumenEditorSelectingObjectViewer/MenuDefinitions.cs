using System.ComponentModel.Composition;
using Gemini.Framework.Menus;
using OngekiFumenEditor.Modules.FumenBulletPalleteListViewer.Commands;
using OngekiFumenEditor.Modules.FumenEditorSelectingObjectViewer.Commands;
using OngekiFumenEditor.Modules.FumenMetaInfoBrowser.Commands;

namespace OngekiFumenEditor.Modules.IFumenEditorSelectingObjectViewer
{
    public static class MenuDefinitions
    {
        [Export]
        public static MenuItemDefinition ViewFumenEditorSelectingObjectViewerMenuItem = new CommandMenuItemDefinition<ViewFumenEditorSelectingObjectViewerCommandDefinition>(
            Gemini.Modules.MainMenu.MenuDefinitions.ViewToolsMenuGroup, 2);
    }
}