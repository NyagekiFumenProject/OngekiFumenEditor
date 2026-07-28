using Gekimini.Avalonia.Framework.Menus;
using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Modules.FumenEditorRenderControlViewer.Commands;

namespace OngekiFumenEditor.Avalonia.Modules.FumenEditorRenderControlViewer;

public static class MenuDefinitions
{
    [RegisterStaticObject]
    public static MenuItemDefinition FumenEditorRenderControlViewerMenuItem = new CommandMenuItemDefinition<FumenEditorRenderControlViewerCommandDefinition>(
        Gekimini.Avalonia.Modules.MainMenu.MenuDefinitions.ViewToolsMenuGroup, 7);
}

