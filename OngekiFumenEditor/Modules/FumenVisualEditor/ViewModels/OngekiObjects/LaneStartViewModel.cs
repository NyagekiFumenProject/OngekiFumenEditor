using Gemini.Modules.Toolbox;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Base.OngekiObjects.Beam;
using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Base.OngekiObjects.Lane;
using OngekiFumenEditor.Base.OngekiObjects.Lane.Base;
using OngekiFumenEditor.Base.OngekiObjects.Wall;
using OngekiFumenEditor.Base.OngekiObjects.Wall.Base;
using OngekiFumenEditor.Modules.FumenVisualEditor.Views.OngekiObjects;
using OngekiFumenEditor.Utils.Attributes;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels.OngekiObjects
{
    [MapToView(ViewType = typeof(LaneStartView))]
    public class LaneStartViewModel<T> : ToolboxGenerator<T> where T : LaneStartBase, new()
    {

    }

    [ToolboxItem(typeof(FumenVisualEditorViewModel), "Lane Left(Red) Start", "Ongeki Lanes")]
    public class LaneLeftStartViewModel : LaneStartViewModel<LaneLeftStart>
    {

    }

    [ToolboxItem(typeof(FumenVisualEditorViewModel), "Lane Center(Green) Start", "Ongeki Lanes")]
    public class LaneCenterStartViewModel : LaneStartViewModel<LaneCenterStart>
    {

    }

    [ToolboxItem(typeof(FumenVisualEditorViewModel), "Lane Right(Blue) Start", "Ongeki Lanes")]
    public class LaneRightStartViewModel : LaneStartViewModel<LaneRightStart>
    {

    }

    [ToolboxItem(typeof(FumenVisualEditorViewModel), "Lane Colorful Start", "Ongeki Lanes")]
    public class LaneColorfulStartViewModel : LaneStartViewModel<ColorfulLaneStart>
    {

    }

    [ToolboxItem(typeof(FumenVisualEditorViewModel), "Enemy Lane Start", "Ongeki Lanes")]
    public class EnemyLaneStartViewModel : LaneStartViewModel<EnemyLaneStart>
    {

    }
}
