using Gekimini.Avalonia.Framework.Menus;
using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Kernel.RecentFiles.Commands;

namespace OngekiFumenEditor.Avalonia.Kernel.RecentFiles;

public static class MenuDefinitions
{
    [RegisterStaticObject]
    public static MenuItemGroupDefinition FileOpenRecentMenuGroup = new(Gekimini.Avalonia.Modules.MainMenu.MenuDefinitions.FileMenu, 9);

    [RegisterStaticObject]
    public static MenuItemDefinition FileRecentFilesMenuItem = new CommandMenuItemDefinition<RecentFilesCommandDefinition>(
        FileOpenRecentMenuGroup, 0);

    [RegisterStaticObject]
    public static MenuItemGroupDefinition FileRecentFilesCascadeGroup = new(
        FileRecentFilesMenuItem, 0);

    [RegisterStaticObject]
    public static MenuItemDefinition FileOpenRecentMenuItemList = new CommandMenuItemDefinition<OpenRecentFileCommandListDefinition>(
        FileRecentFilesCascadeGroup, 0);
}

