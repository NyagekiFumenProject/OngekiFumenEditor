using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects.Lane;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects.Lane.Base;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.ViewModels;

namespace OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Toolboxes.OngekiObjects
{
	public class LaneStartToolboxGenerator<T> : ToolboxGenerator<T> where T : LaneStartBase, new()
	{

	}

	[RegisterTransient<IToolboxGenerator>]
	public class LaneLeftStartToolboxGenerator : LaneStartToolboxGenerator<LaneLeftStart>
	{

	}

	[RegisterTransient<IToolboxGenerator>]
	public class LaneCenterStartToolboxGenerator : LaneStartToolboxGenerator<LaneCenterStart>
	{

	}

	[RegisterTransient<IToolboxGenerator>]
	public class LaneRightStartToolboxGenerator : LaneStartToolboxGenerator<LaneRightStart>
	{

	}

	[RegisterTransient<IToolboxGenerator>]
	public class LaneColorfulStartToolboxGenerator : LaneStartToolboxGenerator<ColorfulLaneStart>
	{

	}

	[RegisterTransient<IToolboxGenerator>]
	public class EnemyLaneStartToolboxGenerator : LaneStartToolboxGenerator<EnemyLaneStart>
	{

	}
}


