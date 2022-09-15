using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels.EditorObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Base.EditorObjects.LaneCurve
{
    public class LaneCurvePathControlObject : OngekiMovableObjectBase
    {
        public int Index { get; set; } = -1;

        private ConnectableChildObjectBase refCurveObject;
        public ConnectableChildObjectBase RefCurveObject
        {
            get => refCurveObject;
            set => Set(ref refCurveObject, value);
        }

        public override Type ModelViewType => typeof(LaneCurvePathControlObjectViewModel);

        public const string CommandName = "[LCO_CTRL]";

        public override string IDShortName => CommandName;

        public override void Copy(OngekiObjectBase fromObj, OngekiFumen fumen)
        {
            base.Copy(fromObj, fumen);

            if (fromObj is not LaneCurvePathControlObject from)
                return;

            Index = from.Index;
        }
    }
}
