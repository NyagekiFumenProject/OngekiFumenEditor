using Gemini.Framework.Menus;
using Injectio.Attributes;

namespace OngekiFumenEditor.Avalonia.Desktop.Modules.FumenVisualEditor.FastOpen;

public static class MenuDefinitions
{
    [RegisterStaticObject]
    public static MenuItemDefinition FastOpenFumenMenuItem = new CommandMenuItemDefinition<FastOpenFumenCommandDefinition>(
        Gekimini.Avalonia.Modules.MainMenu.MenuDefinitions.FileNewOpenMenuGroup, 8);
}
