using System.ComponentModel.Composition;
using Gemini.Framework.Menus;
using OngekiFumenEditor.Modules.AudioPlayerToolViewer.Commands;
using OngekiFumenEditor.Modules.FumenBulletPalleteListViewer.Commands;
using OngekiFumenEditor.Modules.FumenMetaInfoBrowser.Commands;
using OngekiFumenEditor.Modules.FumenVisualEditor.Commands.OgkrImpl.AppendEndLaneObject;
using OngekiFumenEditor.Modules.FumenVisualEditor.Commands.OgkrImpl.FastOpenFumen;
using OngekiFumenEditor.Modules.FumenVisualEditor.Commands.OgkrImpl.InterpolateAll;
using OngekiFumenEditor.Modules.FumenVisualEditor.Commands.OgkrImpl.StandardizeFormat;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Commands.OgkrImpl
{
    public static class MenuDefinitions
    {
        [Export]
        public static MenuDefinition OngekiFumenMenu = new MenuDefinition(Gemini.Modules.MainMenu.MenuDefinitions.MainMenuBar, 6, "音击谱面 (_O)");

        [Export]
        public static MenuItemGroupDefinition OngekiFumenMenuGroup = new MenuItemGroupDefinition(OngekiFumenMenu, 0);

        [Export]
        public static MenuItemDefinition InterpolateAllMenuItem = new CommandMenuItemDefinition<InterpolateAllCommandDefinition>(OngekiFumenMenuGroup, 0);

        [Export]
        public static MenuItemDefinition InterpolateAllWithXGridLimitCommandDefinitionMenuItem = new CommandMenuItemDefinition<InterpolateAllWithXGridLimitCommandDefinition>(OngekiFumenMenuGroup, 0);

        [Export]
        public static MenuItemDefinition StandardizeFormatMenuItem = new CommandMenuItemDefinition<StandardizeFormatCommandDefinition>(OngekiFumenMenuGroup, 1);

        [Export]
        public static MenuItemDefinition AppendEndLaneObjectMenuItem = new CommandMenuItemDefinition<AppendEndLaneObjectCommandDefinition>(OngekiFumenMenuGroup, 0);

        [Export]
        public static MenuItemDefinition FastOpenFumenMenuItem = new CommandMenuItemDefinition<FastOpenFumenCommandDefinition>(Gemini.Modules.MainMenu.MenuDefinitions.FileNewOpenMenuGroup, 8);
    }
}