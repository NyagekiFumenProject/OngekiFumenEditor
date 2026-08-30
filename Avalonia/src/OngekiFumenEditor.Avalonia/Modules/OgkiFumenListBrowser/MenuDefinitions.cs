using Gekimini.Avalonia.Framework.Menus;
using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Modules.OgkiFumenListBrowser.Commands;

namespace OngekiFumenEditor.Avalonia.Modules.OgkiFumenListBrowser;

public static class MenuDefinitions
{
    [RegisterStaticObject]
    public static MenuItemDefinition ViewOgkiFumenListBrowserMenuItem =
        new CommandMenuItemDefinition<ViewOgkiFumenListBrowserCommandDefinition>(
            Gekimini.Avalonia.Modules.MainMenu.MenuDefinitions.FileNewOpenMenuGroup, 2);
}
