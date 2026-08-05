using Gekimini.Avalonia.Modules.Toolbox.Models;
using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects.Lane;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects.Lane.Base;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.ViewModels;

namespace OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Toolboxes.OngekiObjects
{
	public class WallStartToolboxGenerator<T> : ToolboxGenerator<T> where T : WallStartBase, new()
	{
		protected WallStartToolboxGenerator(string name) : base(name, "Ongeki Lanes")
		{
		}
	}

	[RegisterSingleton<ToolboxItem>]
	public class WallLeftStartToolboxGenerator : WallStartToolboxGenerator<WallLeftStart>
	{
		public WallLeftStartToolboxGenerator() : base("Wall Left Start")
		{
		}
	}

	[RegisterSingleton<ToolboxItem>]
	public class WallRightStartToolboxGenerator : WallStartToolboxGenerator<WallRightStart>
	{
		public WallRightStartToolboxGenerator() : base("Wall Right Start")
		{
		}
	}
}


