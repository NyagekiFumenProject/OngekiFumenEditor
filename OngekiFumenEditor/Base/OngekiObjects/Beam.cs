using Caliburn.Micro;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Base.OngekiObjects
{
    public class Beam : OngekiTimelineObjectBase, IHorizonPositionObject, IDisplayableObject
    {
        public class BeamTrack : PropertyChangedBase
        {
            private XGrid xGrid = new XGrid();
            public XGrid XGrid
            {
                get { return xGrid; }
                set
                {
                    xGrid = value;
                    NotifyOfPropertyChange(() => XGrid);
                }
            }

            private TGrid tGrid = new TGrid();
            public TGrid TGrid
            {
                get { return tGrid; }
                set
                {
                    tGrid = value;
                    NotifyOfPropertyChange(() => TGrid);
                }
            }

            private int widthId = 2;
            public int WidthId
            {
                get { return widthId; }
                set
                {
                    widthId = value;
                    NotifyOfPropertyChange(() => WidthId);
                }
            }
        }

        public SortedList<TGrid, BeamTrack> Tracks { get; } = new ();

        public Type ModelViewType => throw new NotImplementedException();

        public XGrid XGrid { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public override string IDShortName => "BEAM";

        public int RecordId { get; set; }

        public override string Serialize(OngekiFumen fumenData)
        {
            throw new NotImplementedException();
        }
    }
}
