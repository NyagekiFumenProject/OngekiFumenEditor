using System.ComponentModel.Composition;
using Gemini.Framework.Menus;
using OngekiFumenEditor.Modules.FumenConverter.Commands;
using OngekiFumenEditor.Modules.OptionGeneratorTools.Commands;

namespace OngekiFumenEditor.Modules.SvgToLaneBrowser
{
    public static class MenuDefintions
    {
        [Export]
        public static MenuItemDefinition ViewMusicXmlWindowMenuItem = new CommandMenuItemDefinition<ViewMusicXmlWindowCommandDefinition>(
            Gemini.Modules.MainMenu.MenuDefinitions.ToolsOptionsMenuGroup, 0);

        [Export]
        public static MenuItemDefinition ViewJacketGeneratorWindowMenuItem = new CommandMenuItemDefinition<ViewJacketGeneratorWindowCommandDefinition>(
            Gemini.Modules.MainMenu.MenuDefinitions.ToolsOptionsMenuGroup, 0);
    }
}