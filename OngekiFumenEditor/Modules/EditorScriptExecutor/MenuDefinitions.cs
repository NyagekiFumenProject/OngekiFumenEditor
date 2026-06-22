using Gemini.Framework.Menus;
using OngekiFumenEditor.Modules.EditorScriptExecutor.Commands;
using System.ComponentModel.Composition;

namespace OngekiFumenEditor.Modules.EditorScriptExecutor
{
    public static class MenuDefinitions
    {
        [Export]
        public static MenuDefinition ScriptsMenu = new MenuDefinition(Gemini.Modules.MainMenu.MenuDefinitions.MainMenuBar, 7, ScriptMenuResources.Scripts);

        [Export]
        public static MenuItemGroupDefinition ScriptsCreateMenuGroup = new MenuItemGroupDefinition(ScriptsMenu, 0);

        [Export]
        public static MenuItemGroupDefinition ScriptsRecommendedMenuGroup = new MenuItemGroupDefinition(ScriptsMenu, 1);

        [Export]
        public static MenuItemGroupDefinition ScriptsRecentMenuGroup = new MenuItemGroupDefinition(ScriptsMenu, 2);

        [Export]
        public static MenuItemDefinition NewScriptMenuItem = new CommandMenuItemDefinition<NewScriptCommandDefinition>(
            ScriptsCreateMenuGroup, 0);

        [Export]
        public static MenuItemDefinition RecommendedScriptsMenuItem = new CommandMenuItemDefinition<RecommendedScriptsCommandDefinition>(
            ScriptsRecommendedMenuGroup, 0);

        [Export]
        public static MenuItemGroupDefinition RecommendedScriptsCascadeGroup = new MenuItemGroupDefinition(
            RecommendedScriptsMenuItem, 0);

        [Export]
        public static MenuItemDefinition RecommendedScriptsMenuItemList = new CommandMenuItemDefinition<OpenRecommendedScriptCommandListDefinition>(
            RecommendedScriptsCascadeGroup, 0);

        [Export]
        public static MenuItemDefinition RecentScriptsMenuItem = new CommandMenuItemDefinition<RecentScriptsCommandDefinition>(
            ScriptsRecentMenuGroup, 0);

        [Export]
        public static MenuItemGroupDefinition RecentScriptsCascadeGroup = new MenuItemGroupDefinition(
            RecentScriptsMenuItem, 0);

        [Export]
        public static MenuItemDefinition RecentScriptsMenuItemList = new CommandMenuItemDefinition<OpenRecentScriptCommandListDefinition>(
            RecentScriptsCascadeGroup, 0);
    }
}
