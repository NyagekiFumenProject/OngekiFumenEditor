using System.ComponentModel.Composition;
using Gemini.Framework.Menus;
using OngekiFumenEditor.Modules.FumenConverter.Commands;

namespace OngekiFumenEditor.Modules.SvgToLaneBrowser
{
    public static class MenuDefintions
    {
        [Export]
        public static MenuItemDefinition ViewFumenConverterMenuItem = new CommandMenuItemDefinition<ViewFumenConverterCommandDefinition>(
            Gemini.Modules.MainMenu.MenuDefinitions.ToolsOptionsMenuGroup, 0);
    }
}