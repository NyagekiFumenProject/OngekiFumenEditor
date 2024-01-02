using Gemini.Framework.Commands;
using OngekiFumenEditor.Properties;
using System;

namespace OngekiFumenEditor.Modules.SplashScreen.Commands.ShowSplashScreen
{
	[CommandDefinition]
	public class ShowSplashScreenCommandDefinition : CommandDefinition
	{
		public override string Name => "Toolbar.ShowSplashScreen";

		public override string Text => "显示启动窗口";

		public override string ToolTip => "显示启动窗口";

		public override Uri IconSource => new Uri("pack://application:,,,/OngekiFumenEditor;component/Resources/Icons/home.png");
	}
}
