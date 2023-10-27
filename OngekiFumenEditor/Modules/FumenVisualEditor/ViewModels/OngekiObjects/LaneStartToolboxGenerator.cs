using Gemini.Modules.Toolbox;
using OngekiFumenEditor.Base.OngekiObjects.Lane;
using OngekiFumenEditor.Base.OngekiObjects.Lane.Base;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels.OngekiObjects
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
