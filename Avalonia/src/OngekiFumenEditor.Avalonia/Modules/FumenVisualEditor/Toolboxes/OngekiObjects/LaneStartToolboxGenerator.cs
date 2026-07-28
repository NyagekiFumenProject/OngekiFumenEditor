using Gekimini.Avalonia.Modules.Toolbox;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects.Lane;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects.Lane.Base;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.ViewModels;

namespace OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Toolboxes.OngekiObjects
{
	public class LaneStartToolboxGenerator<T> : ToolboxGenerator<T> where T : LaneStartBase, new()
	{

	}

	[ToolboxItem(typeof(FumenVisualEditorViewModel), "Lane Left(Red) Start", "Ongeki Lanes")]
	public class LaneLeftStartToolboxGenerator : LaneStartToolboxGenerator<LaneLeftStart>
	{

	}

	[ToolboxItem(typeof(FumenVisualEditorViewModel), "Lane Center(Green) Start", "Ongeki Lanes")]
	public class LaneCenterStartToolboxGenerator : LaneStartToolboxGenerator<LaneCenterStart>
	{

	}

	[ToolboxItem(typeof(FumenVisualEditorViewModel), "Lane Right(Blue) Start", "Ongeki Lanes")]
	public class LaneRightStartToolboxGenerator : LaneStartToolboxGenerator<LaneRightStart>
	{

	}

	[ToolboxItem(typeof(FumenVisualEditorViewModel), "Lane Colorful Start", "Ongeki Lanes")]
	public class LaneColorfulStartToolboxGenerator : LaneStartToolboxGenerator<ColorfulLaneStart>
	{

	}

	[ToolboxItem(typeof(FumenVisualEditorViewModel), "Enemy Lane Start", "Ongeki Lanes")]
	public class EnemyLaneStartToolboxGenerator : LaneStartToolboxGenerator<EnemyLaneStart>
	{

	}
}


