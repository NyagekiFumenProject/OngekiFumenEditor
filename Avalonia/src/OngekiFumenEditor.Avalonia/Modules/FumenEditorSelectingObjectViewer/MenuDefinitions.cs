using Gekimini.Avalonia.Framework.Menus;
using OngekiFumenEditor.Avalonia.Modules.FumenEditorSelectingObjectViewer.Commands;
using Injectio.Attributes;

namespace OngekiFumenEditor.Avalonia.Modules.FumenEditorSelectingObjectViewer
{
	public static class MenuDefinitions
	{
		[RegisterStaticObject]
		public static MenuItemDefinition ViewFumenEditorSelectingObjectViewerMenuItem = new CommandMenuItemDefinition<ViewFumenEditorSelectingObjectViewerCommandDefinition>(
			Gekimini.Avalonia.Modules.MainMenu.MenuDefinitions.ViewToolsMenuGroup, 2);
	}
}


