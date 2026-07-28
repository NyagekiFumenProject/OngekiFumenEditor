using Gekimini.Avalonia.Framework.Menus;
using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Kernel.EditorLayout.Commands.ApplySuggestEditorLayout;

namespace OngekiFumenEditor.Avalonia.Kernel.EditorLayout;

public static class MenuDefinitions
{
    [RegisterStaticObject]
    public static MenuItemGroupDefinition EditorLayoutMenuGroup =
        new(Gekimini.Avalonia.Modules.MainMenu.MenuDefinitions.WindowMenu, 999);

    [RegisterStaticObject]
    public static MenuItemDefinition ApplySuggestEditorLayoutMenuItem =
        new CommandMenuItemDefinition<ApplySuggestEditorLayoutCommandDefinition>(EditorLayoutMenuGroup, 0);
}

