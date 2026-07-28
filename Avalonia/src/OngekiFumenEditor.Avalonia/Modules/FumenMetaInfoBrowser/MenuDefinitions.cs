using Gekimini.Avalonia.Framework.Menus;
using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Modules.FumenMetaInfoBrowser.Commands;

namespace OngekiFumenEditor.Avalonia.Modules.FumenMetaInfoBrowser;

public static class MenuDefinitions
{
    [RegisterStaticObject]
    public static MenuItemDefinition ViewFumenMetaInfoBrowserMenuItem =
        new CommandMenuItemDefinition<ViewFumenMetaInfoBrowserCommandDefinition>(
            Gekimini.Avalonia.Modules.MainMenu.MenuDefinitions.ViewToolsMenuGroup, 2);
}

