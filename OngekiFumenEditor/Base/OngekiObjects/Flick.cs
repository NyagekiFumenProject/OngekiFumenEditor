using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels.OngekiObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Base.OngekiObjects
{
    public class Flick : OngekiTimelineObjectBase, IHorizonPositionObject
    {
        public enum FlickDirection
        {
            Left = 1,
            Right = -1
        }

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

        private FlickDirection direction = FlickDirection.Left;
        public FlickDirection Direction
        {
            get { return direction; }
            set
            {
                direction = value;
                NotifyOfPropertyChange(() => Direction);
            }
        }

        private bool isCritical = false;
        public bool IsCritical
        {
            get { return isCritical; }
            set
            {
                isCritical = value;
                NotifyOfPropertyChange(() => IDShortName);
                NotifyOfPropertyChange(() => IsCritical);
            }
        }

        public override Type ModelViewType => typeof(FlickViewModel);

        public override string IDShortName => IsCritical ? "CFK" : "FLK";

        public override string Serialize(OngekiFumen fumenData)
        {
            return $"{IDShortName} {TGrid.Serialize(fumenData)} {XGrid.Serialize(fumenData)} {(Direction == FlickDirection.Left ? "L" : "R")}";
        }
    }
}
