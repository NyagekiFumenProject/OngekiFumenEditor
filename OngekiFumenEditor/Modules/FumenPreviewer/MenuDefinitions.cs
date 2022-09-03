using System.ComponentModel.Composition;
using Gemini.Framework.Menus;
using OngekiFumenEditor.Modules.FumenConverter.Commands;
using OngekiFumenEditor.Modules.FumenPreviewer.Commands;

namespace OngekiFumenEditor.Modules.FumenPreviewer
{
    public static class MenuDefinitions
    {
        [Export]
        public static MenuItemDefinition ViewFumenPreviewerMenuItem = new CommandMenuItemDefinition<ViewFumenPreviewerCommandDefinition>(
            Gemini.Modules.MainMenu.MenuDefinitions.ViewToolsMenuGroup, 2);
    }
}