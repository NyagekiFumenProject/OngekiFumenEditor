using Gemini.Framework.ToolBars;
using OngekiFumenEditor.Modules.FumenVisualEditor.Commands.EditorModeSwitch;
using OngekiFumenEditor.Modules.FumenVisualEditor.Commands.ShowCurveControlAlways;
using OngekiFumenEditor.Modules.SplashScreen.Commands.ShowSplashScreen;
using System.ComponentModel.Composition;

namespace OngekiFumenEditor.Modules.SplashScreen.Commands
{
	public static class ToolBarDefinitions
	{
		[Export]
		public static ToolBarDefinition SplashScreenToolBar = new ToolBarDefinition(7, "SplashScreen");

		[Export]
		public static ToolBarItemGroupDefinition SplashScreenToolBarGroup = new ToolBarItemGroupDefinition(SplashScreenToolBar, 0);

		[Export]
		public static ToolBarItemDefinition ShowSplashScreenToolBarItem = new CommandToolBarItemDefinition<ShowSplashScreenCommandDefinition>(
			SplashScreenToolBarGroup, 0);
	}
}
