using OngekiFumenEditor.Base.OngekiObjects.Lane;
using OngekiFumenEditor.Base.OngekiObjects.Lane.Base;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels.OngekiObjects
{
    public class LaneEndViewModel<T> : ConnectableChildBaseViewModel<T> where T : LaneEndBase, new()
    {

    }

    public class LaneCenterEndViewModel: LaneEndViewModel<LaneCenterEnd> { }
    public class LaneLeftEndViewModel : LaneEndViewModel<LaneLeftEnd> { }
    public class LaneRightEndViewModel : LaneEndViewModel<LaneRightEnd> { }
    public class LaneColorfulEndViewModel : LaneEndViewModel<ColorfulLaneEnd> { }
}
