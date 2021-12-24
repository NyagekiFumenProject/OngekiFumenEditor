using System.ComponentModel.Composition;
using Gemini.Framework.Menus;
using OngekiFumenEditor.Modules.FumenMetaInfoBrowser.Commands;
using OngekiFumenEditor.Modules.FumenVisualEditorSettings.Commands;

namespace OngekiFumenEditor.Modules.FumenVisualEditorSettings
{
    public static class MenuDefinitions
    {
        [Export]
        public static MenuItemDefinition ViewFumenMetaInfoBrowserMenuItem = new CommandMenuItemDefinition<ViewFumenVisualEditorSettingsCommandDefinition>(
            Gemini.Modules.MainMenu.MenuDefinitions.ViewToolsMenuGroup, 2);
    }
}