using Gekimini.Avalonia.Framework.Menus;
using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Modules.FumenConverter.Commands;

namespace OngekiFumenEditor.Avalonia.Modules.FumenConverter;

public static class MenuDefinitions
{
    [RegisterStaticObject]
    public static MenuItemDefinition ViewFumenConverterMenuItem = new CommandMenuItemDefinition<ViewFumenConverterCommandDefinition>(
        Gekimini.Avalonia.Modules.MainMenu.MenuDefinitions.ToolsOptionsMenuGroup, 1);
}
