using Gemini.Framework.Menus;
using OngekiFumenEditor.Modules.AudioPlayerToolViewer.Commands;
using System.ComponentModel.Composition;

namespace OngekiFumenEditor.Modules.AudioPlayerToolViewer
{
	public static class MenuDefinitions
	{
		[Export]
		public static MenuItemDefinition ViewFumenMetaInfoBrowserMenuItem = new CommandMenuItemDefinition<ViewAudioPlayerToolViewerCommandDefinition>(
			Gemini.Modules.MainMenu.MenuDefinitions.ViewToolsMenuGroup, 2);
	}
}