using OngekiFumenEditor.Base.EditorObjects;
using OngekiFumenEditor.Modules.FumenObjectPropertyBrowser.Attrbutes;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels.OngekiObjects;
using System;
using System.Collections.Generic;

namespace OngekiFumenEditor.Base.OngekiObjects
{
    //[DontShowPropertyInfoAttrbute]
    public class Bullet : OngekiTimelineObjectBase, IHorizonPositionObject
    {
        private XGrid xGrid = new XGrid();
        public XGrid XGrid
        {
            get
            {
                return xGrid;
            }
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

        private BulletAuxiliaryLine line;

        public Bullet()
        {
            line = new BulletAuxiliaryLine(this);
        }

        public override IEnumerable<IDisplayableObject> GetDisplayableObjects()
        {
            yield return line;
            yield return this;
        }

        public override Type ModelViewType => typeof(BulletViewModel);

        public override string IDShortName => "BLT";

        public override string Serialize(OngekiFumen fumenData)
        {
            return $"{IDShortName} {ReferenceBulletPallete?.StrID} {TGrid.Serialize(fumenData)} {XGrid.Serialize(fumenData)}";
        }

        public override string ToString() => $"{base.ToString()} Pallete:({ReferenceBulletPallete})";
    }
}
