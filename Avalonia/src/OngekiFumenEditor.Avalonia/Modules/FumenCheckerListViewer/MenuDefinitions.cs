using Gekimini.Avalonia.Framework.Menus;
using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Modules.FumenCheckerListViewer.Commands;

namespace OngekiFumenEditor.Avalonia.Modules.FumenCheckerListViewer;

public static class MenuDefinitions
{
    [RegisterStaticObject]
    public static MenuItemDefinition ViewFumenCheckerListViewerMenuItem = new CommandMenuItemDefinition<ViewFumenCheckerListViewerCommandDefinition>(
        Gekimini.Avalonia.Modules.MainMenu.MenuDefinitions.ViewToolsMenuGroup, 3);
}
