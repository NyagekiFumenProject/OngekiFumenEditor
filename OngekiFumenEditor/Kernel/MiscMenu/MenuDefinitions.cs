using System.ComponentModel.Composition;
using Gemini.Framework.Menus;
using OngekiFumenEditor.Kernel.MiscMenu.Commands;
using OngekiFumenEditor.Modules.AudioPlayerToolViewer.Commands;
using OngekiFumenEditor.Modules.FumenBulletPalleteListViewer.Commands;
using OngekiFumenEditor.Modules.FumenMetaInfoBrowser.Commands;

namespace OngekiFumenEditor.Kernel.MiscMenu
{
    public static class MenuDefinitions
    {
        [Export]
        public static MenuItemGroupDefinition ProgramMiscOpenMenuGroup = new MenuItemGroupDefinition(Gemini.Modules.MainMenu.MenuDefinitions.FileMenu, 8);

        [Export]
        public static MenuItemDefinition ViewFumenMetaInfoBrowserMenuItem = new CommandMenuItemDefinition<CallFullGCCommandDefinition>(
            ProgramMiscOpenMenuGroup, 0);
    }
}