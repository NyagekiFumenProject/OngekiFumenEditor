using Gekimini.Avalonia.Framework.Menus;
using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Modules.FumenTimeSignatureListViewer.Commands;

namespace OngekiFumenEditor.Avalonia.Modules.FumenTimeSignatureListViewer;

public static class MenuDefinitions
{
    [RegisterStaticObject]
    public static MenuItemDefinition ViewFumenTimeSignatureListViewerMenuItem = new CommandMenuItemDefinition<ViewFumenTimeSignatureListViewerCommandDefinition>(
        Gekimini.Avalonia.Modules.MainMenu.MenuDefinitions.ViewToolsMenuGroup, 4);
}
