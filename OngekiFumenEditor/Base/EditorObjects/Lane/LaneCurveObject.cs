using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Base.OngekiObjects.Lane.Base;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels.EditorObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Base.EditorObjects.Lane
{
    public class LaneCurveObject : ConnectableChildObjectBase
    {
        private TGrid midControl = default;
        public TGrid MidControl
        {
            get => midControl ?? GetDefaultMidControlValue();
            set => Set(ref midControl, value);
        }

        private TGrid GetDefaultMidControlValue()
        {
            if (PrevObject is null)
                return TGrid;
            return TGrid + new GridOffset(0, -(TGrid - PrevObject.TGrid).TotalGrid(TGrid.ResT) / 2);
        }

        public LaneType LaneType => (ReferenceStartObject as LaneStartBase)?.LaneType ?? LaneType.Center;

        public override Type ModelViewType => typeof(LaneCurveObjectViewModel);

        public override string IDShortName => "LCO";
    }
}
