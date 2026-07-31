using Gekimini.Avalonia.Framework.Menus;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Commands.OgkrImpl.FastOpenFumen;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Commands.OgkrImpl.InterpolateAll;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Commands.OgkrImpl.StandardizeFormat;
using OngekiFumenEditor.Avalonia.Assets.Languages;
using Injectio.Attributes;

namespace OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Commands.OgkrImpl
{
	public static class MenuDefinitions
	{
		[RegisterStaticObject]
		public static MenuDefinition OngekiFumenMenu = new MenuDefinition(Gekimini.Avalonia.Modules.MainMenu.MenuDefinitions.MainMenuBar, 6, Lang.B.MenuOngeki.ToLocalizedString());

		[RegisterStaticObject]
		public static MenuItemGroupDefinition OngekiFumenMenuGroup = new MenuItemGroupDefinition(OngekiFumenMenu, 0);

		[RegisterStaticObject]
		public static MenuItemDefinition InterpolateAllMenuItem = new CommandMenuItemDefinition<InterpolateAllCommandDefinition>(OngekiFumenMenuGroup, 0);

		[RegisterStaticObject]
		public static MenuItemDefinition InterpolateAllWithXGridLimitCommandDefinitionMenuItem = new CommandMenuItemDefinition<InterpolateAllWithXGridLimitCommandDefinition>(OngekiFumenMenuGroup, 0);

		[RegisterStaticObject]
		public static MenuItemDefinition StandardizeFormatMenuItem = new CommandMenuItemDefinition<StandardizeFormatCommandDefinition>(OngekiFumenMenuGroup, 1);

		[RegisterStaticObject]
		public static MenuItemDefinition FastOpenFumenMenuItem = new CommandMenuItemDefinition<FastOpenFumenCommandDefinition>(Gekimini.Avalonia.Modules.MainMenu.MenuDefinitions.FileNewOpenMenuGroup, 8);
	}
}



