using Gemini.Framework.Menus;
using OngekiFumenEditor.Kernel.EditorLayout.Commands.ApplySuggestEditorLayout;
using System.ComponentModel.Composition;

namespace OngekiFumenEditor.Kernel.EditorLayout
{
    public static class MenuDefinitions
    {
        [Export]
        public static MenuItemGroupDefinition EditorLayoutMenuGroup = new MenuItemGroupDefinition(Gemini.Modules.MainMenu.MenuDefinitions.WindowMenu, 999);

        [Export]
        public static MenuItemDefinition ApplySuggestEditorLayoutMenuItem = new CommandMenuItemDefinition<ApplySuggestEditorLayoutCommandDefinition>(
            EditorLayoutMenuGroup, 0);
    }
}