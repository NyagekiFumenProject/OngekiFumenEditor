using Gekimini.Avalonia.Framework.ToolBars;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Commands.EditorModeSwitch;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Commands.ShowCurveControlAlways;
using OngekiFumenEditor.Avalonia.Modules.SplashScreen.Commands.ShowSplashScreen;
using Injectio.Attributes;

namespace OngekiFumenEditor.Avalonia.Modules.SplashScreen.Commands
{
	public static class ToolBarDefinitions
	{
		[RegisterStaticObject]
		public static ToolBarDefinition SplashScreenToolBar = new ToolBarDefinition(7, "SplashScreen");

		[RegisterStaticObject]
		public static ToolBarItemGroupDefinition SplashScreenToolBarGroup = new ToolBarItemGroupDefinition(SplashScreenToolBar, 0);

		[RegisterStaticObject]
		public static ToolBarItemDefinition ShowSplashScreenToolBarItem = new CommandToolBarItemDefinition<ShowSplashScreenCommandDefinition>(
			SplashScreenToolBarGroup, 0);
	}
}



