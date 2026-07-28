using Gekimini.Avalonia.Framework.Menus;
using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Modules.AudioAdjustWindow.Commands;

namespace OngekiFumenEditor.Avalonia.Modules.AudioAdjustWindow;

public static class MenuDefinitions
{
    [RegisterStaticObject]
    public static MenuItemDefinition ViewAudioAdjustWindowMenuItem = new CommandMenuItemDefinition<ViewAudioAdjustWindowCommandDefinition>(
        Gekimini.Avalonia.Modules.MainMenu.MenuDefinitions.ToolsOptionsMenuGroup, 0);
}
