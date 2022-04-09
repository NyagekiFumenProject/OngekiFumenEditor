using OngekiFumenEditor.Base.OngekiObjects.Beam;
using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Base.OngekiObjects.Wall;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels.OngekiObjects
{
    public abstract class ConnectableChildBaseViewModel<T> : ConnectableBaseViewModel<T> where T : ConnectableChildObjectBase, new()
    {
        public override bool IsSelected
        {
            get => ((ConnectableChildObjectBase)ReferenceOngekiObject).IsSelecting;
            set
            {
                ((ConnectableChildObjectBase)ReferenceOngekiObject).IsSelecting = value;
                //EditorViewModel?.OnSelectPropertyChanged(this, value);
                NotifyOfPropertyChange(() => IsSelected);
            }
        }

        public override double? CheckAndAdjustY(double y)
        {
            var result = base.CheckAndAdjustY(y);
            if (result is null)
                return null;
            y = result ?? 0;

            if (ReferenceOngekiObject is ConnectableChildObjectBase childBase && childBase.PrevObject is ConnectableObjectBase prevWall)
            {
                var prevY = TGridCalculator.ConvertTGridToY(prevWall.TGrid, EditorViewModel);
                if (prevY > y)
                {
                    y = prevY;
                }
            }

            return y;
        }
    }
}
