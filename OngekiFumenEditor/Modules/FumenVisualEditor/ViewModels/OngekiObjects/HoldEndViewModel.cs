using OngekiFumenEditor.Base.OngekiObjects;
using System;
using System.Collections.Generic;
using Gemini.Modules.Toolbox;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Utils;
using OngekiFumenEditor.Base.OngekiObjects.Lane.Base;
using OngekiFumenEditor.Utils.Attributes;
using OngekiFumenEditor.Modules.FumenVisualEditor.Views.OngekiObjects;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels.OngekiObjects
{
    [MapToView(ViewType = typeof(TapView))]
    public class HoldEndViewModel : LaneDockableViewModelBase<HoldEnd>
    {
        public override IEnumerable<ConnectableObjectBase> PickDockableObjects(FumenVisualEditorViewModel editor = null)
        {
            return Enumerable.Empty<ConnectableObjectBase>();
        }

        public override void MoveCanvas(Point relativePoint)
        {
            var editor = EditorViewModel;
            var tGrid = TGridCalculator.ConvertYToTGrid(CheckAndAdjustY(relativePoint.Y), editor);
            if (tGrid is not null)
            {
                if (((ReferenceOngekiObject as HoldEnd)?.ReferenceStartObject as Hold)?.ReferenceLaneStart is LaneStartBase start)
                {
                    var x = CalculateConnectableObjectCurrentRelativeX(start, tGrid) ?? relativePoint.X;
                    relativePoint.X = x;
                    //Log.LogDebug($"auto lock to lane x: {x}");
                }
            }

            base.MoveCanvas(relativePoint);
        }

        public override double CheckAndAdjustX(double x)
        {
            return x;
        }

        public override double CheckAndAdjustY(double y)
        {
            return y;
        }
    }
}
