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
    public class LaneNextViewModel<T> : ConnectableChildBaseViewModel<T> where T : LaneNextBase, new()
    {

    }

    public class LaneRightNextViewModel : LaneNextViewModel<LaneRightNext> { }
    public class LaneCenterNextViewModel : LaneNextViewModel<LaneCenterNext> { }
    public class LaneLeftNextViewModel : LaneNextViewModel<LaneLeftNext> { }
    public class LaneColorfulNextViewModel : LaneNextViewModel<ColorfulLaneNext> { }
}
