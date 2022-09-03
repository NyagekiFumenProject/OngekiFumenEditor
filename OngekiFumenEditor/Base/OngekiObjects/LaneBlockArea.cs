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
        public enum BlockDirection
        {
            Left = 1,
            Right = -1
        }

        public class LaneBlockAreaEndIndicator : OngekiTimelineObjectBase
        {
            public override Type ModelViewType => typeof(LaneBlockAreaEndIndicatorViewModel);

            public override string IDShortName => "[LBK_End]";

            public LaneBlockArea RefLaneBlockArea { get; internal protected set; }

            public override IEnumerable<IDisplayableObject> GetDisplayableObjects() => IDisplayableObject.EmptyDisplayable;

            public override TGrid TGrid
            {
                get => base.TGrid.TotalGrid <= 0 ? (TGrid = RefLaneBlockArea.TGrid.CopyNew()) : base.TGrid;
                set => base.TGrid = value;
            }

            public override string ToString() => $"{base.ToString()}";
        }

        private IDisplayableObject[] displayables;

        public LaneBlockArea()
        {
            EndIndicator = new LaneBlockAreaEndIndicator() { RefLaneBlockArea = this };
            EndIndicator.PropertyChanged += EndIndicator_PropertyChanged;
            connector = new LaneBlockLaneDecoration() { From = this, To = EndIndicator };
            displayables = new IDisplayableObject[] { connector, this, EndIndicator };
        }

        private void EndIndicator_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            NotifyOfPropertyChange(() => e.PropertyName);
        }

        public override IEnumerable<IDisplayableObject> GetDisplayableObjects() => displayables;

        public override string IDShortName => "LBK";

        private LaneBlockLaneDecoration connector;
        public LaneBlockAreaEndIndicator EndIndicator { get; }

        private BlockDirection direction = BlockDirection.Left;
        public BlockDirection Direction
        {
            get => direction;
            set => Set(ref direction, value);
        }

        public override Type ModelViewType => typeof(LaneBlockAreaViewModel);

        public (LaneStartBase startWallLane, LaneStartBase endWallLane) CalculateReferenceWallLanes(OngekiFumen fumen)
        {
            var wallType = Direction == BlockDirection.Left ? LaneType.WallLeft : LaneType.WallRight;

            var blockStartTGrid = TGrid;
            var blockEndTGrid = EndIndicator.TGrid;

            var startWallLane = fumen.Lanes.Where(x => x.LaneType == wallType).LastOrDefault(x => x.TGrid <= blockStartTGrid);
            var endWallLane = fumen.Lanes.Where(x => x.LaneType == wallType).LastOrDefault(x => x.TGrid <= blockEndTGrid);

            return (startWallLane, endWallLane);
        }

        public override string ToString() => $"{base.ToString()} {Direction} End:({EndIndicator})";

        public IEnumerable<LaneStartBase> GetAffactableWallLanes(OngekiFumen fumen)
        {
            var blockStartTGrid = TGrid;
            var blockEndTGrid = EndIndicator.TGrid;
            var wallType = Direction == BlockDirection.Left ? LaneType.WallLeft : LaneType.WallRight;

            return fumen.Lanes.Where(x =>
                x.LaneType == wallType &&
                x.MaxTGrid > blockStartTGrid &&
                x.MinTGrid < blockEndTGrid
            );
        }

        public override bool CheckVisiable(TGrid minVisibleTGrid, TGrid maxVisibleTGrid)
        {
            if (maxVisibleTGrid < TGrid)
                return false;

            if (EndIndicator.TGrid < minVisibleTGrid)
                return false;

            return true;
        }
    }
}
