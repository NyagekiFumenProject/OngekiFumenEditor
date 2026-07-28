using Gekimini.Avalonia.Framework.Menus;
using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Modules.AudioPlayerToolViewer.Commands;

namespace OngekiFumenEditor.Avalonia.Modules.AudioPlayerToolViewer;

public static class MenuDefinitions
{
    [RegisterStaticObject]
    public static MenuItemDefinition ViewAudioPlayerToolViewerMenuItem = new CommandMenuItemDefinition<ViewAudioPlayerToolViewerCommandDefinition>(
        Gekimini.Avalonia.Modules.MainMenu.MenuDefinitions.ViewToolsMenuGroup, 2);
}
