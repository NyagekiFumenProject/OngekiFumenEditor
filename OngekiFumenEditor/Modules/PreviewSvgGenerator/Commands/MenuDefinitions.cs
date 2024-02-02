using Gemini.Framework.Menus;
using OngekiFumenEditor.Modules.PreviewSvgGenerator.Commands.GenerateSvg;
using System.ComponentModel.Composition;

namespace OngekiFumenEditor.Modules.PreviewSvgGenerator.Commands
{
    public static class MenuDefinitions
    {
        [Export]
        public static MenuItemDefinition GenerateSvgMenuItem = new CommandMenuItemDefinition<GenerateSvgCommandDefinition>(
            Gemini.Modules.MainMenu.MenuDefinitions.ToolsOptionsMenuGroup, 2);
    }
}
