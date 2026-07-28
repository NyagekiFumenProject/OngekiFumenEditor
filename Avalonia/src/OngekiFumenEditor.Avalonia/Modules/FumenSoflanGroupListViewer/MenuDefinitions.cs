using Gekimini.Avalonia.Framework.Menus;
using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Modules.FumenSoflanGroupListViewer.Commands;

namespace OngekiFumenEditor.Avalonia.Modules.FumenSoflanGroupListViewer;

public static class MenuDefinitions
{
    [RegisterStaticObject]
    public static MenuItemDefinition FumenSoflanGroupListViewerMenuItem = new CommandMenuItemDefinition<FumenSoflanGroupListViewerCommandDefinition>(
        Gekimini.Avalonia.Modules.MainMenu.MenuDefinitions.ViewToolsMenuGroup, 5);
}
