using OngekiFumenEditor.Base.EditorObjects;
using OngekiFumenEditor.Base.OngekiObjects.Lane.Base;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels.OngekiObjects;
using OngekiFumenEditor.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Base.OngekiObjects
{
    public class LaneBlockArea : OngekiTimelineObjectBase
    {
        public class LaneBlockAreaEndIndicator : OngekiTimelineObjectBase
        {
            public override Type ModelViewType => typeof(LaneBlockAreaEndIndicatorViewModel);

            public override string IDShortName => "[LBK_End]";

            public LaneBlockArea RefLaneBlockArea { get; internal protected set; }

            public override IEnumerable<IDisplayableObject> GetDisplayableObjects() => IDisplayableObject.EmptyDisplayable;

            public override TGrid TGrid
            {
                get => base.TGrid.TotalGrid <= 0 ? RefLaneBlockArea.TGrid : base.TGrid;
                set => base.TGrid = value;
            }
        }

        private IDisplayableObject[] displayables;

        public LaneBlockArea()
        {
            EndIndicator = new LaneBlockAreaEndIndicator() { RefLaneBlockArea = this };
            //connector = new LaneBlockAreaConnector() { From = this, To = EndIndicator };
            displayables = new IDisplayableObject[] { /*connector, */this, EndIndicator };
        }

        public override IEnumerable<IDisplayableObject> GetDisplayableObjects() => displayables;

        public override string IDShortName => "LBK";

        //private LaneBlockAreaConnector connector;
        public LaneBlockAreaEndIndicator EndIndicator { get; }

        public override Type ModelViewType => typeof(LaneBlockAreaViewModel);

        public (LaneStartBase startWallLane, LaneStartBase endWallLane) CalculateReferenceWallLanes(OngekiFumen fumen)
        {
            var blockStartTGrid = TGrid;
            var blockEndTGrid = EndIndicator.TGrid;

            var startWallLane = fumen.Lanes.Where(x => x.IsWallLane).LastOrDefault(x => x.TGrid <= blockStartTGrid);
            var endWallLane = fumen.Lanes.Where(x => x.IsWallLane).LastOrDefault(x => x.TGrid <= blockEndTGrid);

            return (startWallLane, endWallLane);
        }
    }
}
