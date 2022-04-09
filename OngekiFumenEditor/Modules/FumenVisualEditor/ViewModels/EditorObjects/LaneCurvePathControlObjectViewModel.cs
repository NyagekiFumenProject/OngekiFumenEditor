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
        public override bool IsSelected 
        { 
            get => ((LaneCurvePathControlObject)ReferenceOngekiObject).IsSelecting;
            set
            {
                ((LaneCurvePathControlObject)ReferenceOngekiObject).IsSelecting = value;
                //EditorViewModel?.OnSelectPropertyChanged(this, value);
                NotifyOfPropertyChange(() => IsSelected);
            }
        }

        public override void OnMouseClick(Point pos)
        {
            IsSelected = true;
        }
    }
}
