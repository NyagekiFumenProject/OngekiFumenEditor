using OngekiFumenEditor.Base.Collections;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels.OngekiObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Base.OngekiObjects
{
    public class Bullet : OngekiTimelineObjectBase, IHorizonPositionObject
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

        private BulletPallete referenceBulletPallete;
        public BulletPallete ReferenceBulletPallete
        {
            get { return referenceBulletPallete; }
            set
            {
                referenceBulletPallete = value;
                NotifyOfPropertyChange(() => ReferenceBulletPallete);
            }
        }

        public override Type ModelViewType => typeof(BulletViewModel);

        public override string IDShortName => "BLT";

        public override string Serialize(OngekiFumen fumenData)
        {
            return $"{IDShortName} {ReferenceBulletPallete?.StrID} {TGrid.Serialize(fumenData)} {XGrid.Serialize(fumenData)}";
        }
    }
}
