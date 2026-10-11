using Gemini.Modules.Toolbox;
using OngekiFumenEditor.Base.OngekiObjects.Lane;
using OngekiFumenEditor.Base.OngekiObjects.Lane.Base;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Toolboxes.OngekiObjects
{
    public class LaneStartToolboxGenerator<T> : ToolboxGenerator<T> where T : LaneStartBase, new()
    {

    }

    [ToolboxItem(typeof(FumenVisualEditorViewModel), "Lane Left(Red) Start", "Ongeki Lanes", description: "ToolboxItemLaneLeftStart")]
    public class LaneLeftStartToolboxGenerator : LaneStartToolboxGenerator<LaneLeftStart>
    {

    }

    [ToolboxItem(typeof(FumenVisualEditorViewModel), "Lane Center(Green) Start", "Ongeki Lanes", description: "ToolboxItemLaneCenterStart")]
    public class LaneCenterStartToolboxGenerator : LaneStartToolboxGenerator<LaneCenterStart>
    {

    }

    [ToolboxItem(typeof(FumenVisualEditorViewModel), "Lane Right(Blue) Start", "Ongeki Lanes", description: "ToolboxItemLaneRightStart")]
    public class LaneRightStartToolboxGenerator : LaneStartToolboxGenerator<LaneRightStart>
    {

    }

    [ToolboxItem(typeof(FumenVisualEditorViewModel), "Lane Colorful Start", "Ongeki Lanes", description: "ToolboxItemLaneColorfulStart")]
    public class LaneColorfulStartToolboxGenerator : LaneStartToolboxGenerator<ColorfulLaneStart>
    {

    }

    [ToolboxItem(typeof(FumenVisualEditorViewModel), "Enemy Lane Start", "Ongeki Lanes", description: "ToolboxItemEnemyLaneStart")]
    public class EnemyLaneStartToolboxGenerator : LaneStartToolboxGenerator<EnemyLaneStart>
    {

    }
}
