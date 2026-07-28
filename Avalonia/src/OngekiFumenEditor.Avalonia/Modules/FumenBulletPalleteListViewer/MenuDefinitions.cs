using Gekimini.Avalonia.Framework.Menus;
using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Modules.FumenBulletPalleteListViewer.Commands;

namespace OngekiFumenEditor.Avalonia.Modules.FumenBulletPalleteListViewer;

public static class MenuDefinitions
{
    [RegisterStaticObject]
    public static MenuItemDefinition ViewFumenBulletPalleteListViewerMenuItem = new CommandMenuItemDefinition<ViewFumenBulletPalleteListViewerCommandDefinition>(
        Gekimini.Avalonia.Modules.MainMenu.MenuDefinitions.ViewToolsMenuGroup, 6);
}

