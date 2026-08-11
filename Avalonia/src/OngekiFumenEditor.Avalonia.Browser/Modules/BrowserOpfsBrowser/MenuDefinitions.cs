#nullable enable

using Gemini.Framework.Menus;
using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Browser.Modules.BrowserOpfsBrowser.Commands;

namespace OngekiFumenEditor.Avalonia.Browser.Modules.BrowserOpfsBrowser;

public static class MenuDefinitions
{
    [RegisterStaticObject]
    public static MenuItemGroupDefinition BrowserOpfsMenuGroup =
        new(Gekimini.Avalonia.Modules.MainMenu.MenuDefinitions.FileMenu, 7);

    [RegisterStaticObject]
    public static MenuItemDefinition BrowseBrowserOpfsMenuItem =
        new CommandMenuItemDefinition<BrowseBrowserOpfsCommandDefinition>(BrowserOpfsMenuGroup, 0);
}
