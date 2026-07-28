using Gekimini.Avalonia.Framework.Menus;
using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Modules.PreviewSvgGenerator.Commands.GenerateSvg;

namespace OngekiFumenEditor.Avalonia.Modules.PreviewSvgGenerator.Commands;

public static class MenuDefinitions
{
    [RegisterStaticObject]
    public static MenuItemDefinition GenerateSvgMenuItem =
        new CommandMenuItemDefinition<GenerateSvgCommandDefinition>(
            Gekimini.Avalonia.Modules.MainMenu.MenuDefinitions.ToolsOptionsMenuGroup, 2);
}

