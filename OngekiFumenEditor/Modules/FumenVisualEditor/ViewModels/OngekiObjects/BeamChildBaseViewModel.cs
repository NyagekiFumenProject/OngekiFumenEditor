using OngekiFumenEditor.Base.OngekiObjects.Beam;
using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels.OngekiObjects
{
    public abstract class BeamChildBaseViewModel<T> : DisplayObjectViewModelBase<T> where T : BeamChildObjectBase, new()
    {
        public override double? CheckAndAdjustY(double y)
        {
            var result = base.CheckAndAdjustY(y);
            if (result is null)
                return null;
            y = result ?? 0;

            if (ReferenceOngekiObject is BeamChildObjectBase childBase && childBase.PrevObject is ConnectableObjectBase prevBeam)
            {
                var prevY = TGridCalculator.ConvertTGridToY(prevBeam.TGrid, EditorViewModel);
                if (prevY > y)
                {
                    y = prevY;
                }
            }

            return y;
        }
    }
}
