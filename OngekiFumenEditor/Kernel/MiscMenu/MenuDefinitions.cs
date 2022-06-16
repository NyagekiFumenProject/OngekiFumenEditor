using System.ComponentModel.Composition;
using Gemini.Framework.Menus;
using OngekiFumenEditor.Kernel.MiscMenu.Commands.About;
using OngekiFumenEditor.Kernel.MiscMenu.Commands.CallFullGC;

namespace OngekiFumenEditor.Kernel.MiscMenu
{
    public static class MenuDefinitions
    {
        [Export]
        public static MenuItemGroupDefinition ProgramMiscOpenMenuGroup = new MenuItemGroupDefinition(Gemini.Modules.MainMenu.MenuDefinitions.FileMenu, 8);

        [Export]
        public static MenuItemDefinition ViewFumenMetaInfoBrowserMenuItem = new CommandMenuItemDefinition<CallFullGCCommandDefinition>(
            ProgramMiscOpenMenuGroup, 0);

        [Export]
        public static MenuDefinition HelpMenu = new MenuDefinition(Gemini.Modules.MainMenu.MenuDefinitions.MainMenuBar, 99999, "帮助");

        [Export]
        public static MenuItemGroupDefinition HelpMenuGroup = new MenuItemGroupDefinition(HelpMenu, 0); 
        [Export]
        public static MenuItemDefinition AboutMenuItem = new CommandMenuItemDefinition<AboutCommandDefinition>(
            HelpMenuGroup, 0);
    }
}