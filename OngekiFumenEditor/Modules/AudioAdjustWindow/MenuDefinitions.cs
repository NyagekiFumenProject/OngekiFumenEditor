using System.ComponentModel.Composition;
using Gemini.Framework.Menus;
using OngekiFumenEditor.Modules.AudioAdjustWindow.Commands;
using OngekiFumenEditor.Modules.FumenConverter.Commands;

namespace OngekiFumenEditor.Modules.AudioAdjustWindow
{
    public static class AudioAdjustWindow
    {
        [Export]
        public static MenuItemDefinition ViewAudioAdjustWindowMenuItem = new CommandMenuItemDefinition<ViewAudioAdjustWindowCommandDefinition>(
            Gemini.Modules.MainMenu.MenuDefinitions.ToolsOptionsMenuGroup, 0);
    }
}