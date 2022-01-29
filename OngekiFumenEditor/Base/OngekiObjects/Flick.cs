using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels.OngekiObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Base.OngekiObjects
{
    public class Flick : OngekiMovableObjectBase
    {
        public enum FlickDirection
        {
            Left = 1,
            Right = -1
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

        public override void Copy(OngekiObjectBase fromObj, OngekiFumen fumen)
        {
            base.Copy(fromObj, fumen);

            if (fromObj is not Flick from)
                return;

            Direction = from.Direction;
            IsCritical = from.IsCritical;
        }
    }
}
