using Gemini.Framework.Menus;
using OngekiFumenEditor.Kernel.EditorLayout.Commands.About;
using OngekiFumenEditor.Kernel.MiscMenu.Commands.About;
using OngekiFumenEditor.Kernel.MiscMenu.Commands.CallFullGC;
using OngekiFumenEditor.Kernel.MiscMenu.Commands.OpenUrlCommon;
using OngekiFumenEditor.Properties;
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