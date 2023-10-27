using Gemini.Framework.Menus;
using OngekiFumenEditor.Modules.OgkiFumenListBrowser.Commands;
using System.ComponentModel.Composition;

namespace OngekiFumenEditor.Modules.OgkiFumenListBrowser
{
	public static class OgkiFumenListBrowser
	{
		[Export]
		public static MenuItemDefinition ViewOgkiFumenListBrowserMenuItem = new CommandMenuItemDefinition<ViewOgkiFumenListBrowserCommandDefinition>(
			Gemini.Modules.MainMenu.MenuDefinitions.FileNewOpenMenuGroup, 2);
	}
}