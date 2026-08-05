using Gekimini.Avalonia.Modules.Toolbox.Models;
using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects.Lane;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects.Lane.Base;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.ViewModels;

namespace OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Toolboxes.OngekiObjects
{
	public class LaneStartToolboxGenerator<T> : ToolboxGenerator<T> where T : LaneStartBase, new()
	{
		protected LaneStartToolboxGenerator(string name) : base(name, "Ongeki Lanes")
		{
		}
	}

	[RegisterSingleton<ToolboxItem>]
	public class LaneLeftStartToolboxGenerator : LaneStartToolboxGenerator<LaneLeftStart>
	{
		public LaneLeftStartToolboxGenerator() : base("Lane Left(Red) Start")
		{
		}
	}

	[RegisterSingleton<ToolboxItem>]
	public class LaneCenterStartToolboxGenerator : LaneStartToolboxGenerator<LaneCenterStart>
	{
		public LaneCenterStartToolboxGenerator() : base("Lane Center(Green) Start")
		{
		}
	}

	[RegisterSingleton<ToolboxItem>]
	public class LaneRightStartToolboxGenerator : LaneStartToolboxGenerator<LaneRightStart>
	{
		public LaneRightStartToolboxGenerator() : base("Lane Right(Blue) Start")
		{
		}
	}

	[RegisterSingleton<ToolboxItem>]
	public class LaneColorfulStartToolboxGenerator : LaneStartToolboxGenerator<ColorfulLaneStart>
	{
		public LaneColorfulStartToolboxGenerator() : base("Lane Colorful Start")
		{
		}
	}

	[RegisterSingleton<ToolboxItem>]
	public class EnemyLaneStartToolboxGenerator : LaneStartToolboxGenerator<EnemyLaneStart>
	{
		public EnemyLaneStartToolboxGenerator() : base("Enemy Lane Start")
		{
		}
	}
}


