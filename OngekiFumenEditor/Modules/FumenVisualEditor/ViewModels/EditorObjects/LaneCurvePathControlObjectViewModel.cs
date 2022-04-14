using OngekiFumenEditor.Base.EditorObjects.LaneCurve;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels.EditorObjects
{
    public class LaneCurvePathControlObjectViewModel : DisplayObjectViewModelBase<LaneCurvePathControlObject>
    {
        public override void OnMouseClick(Point pos)
        {
            ((LaneCurvePathControlObject)ReferenceOngekiObject).IsSelected = true;
            base.OnMouseClick(pos);
        }
    }
}
