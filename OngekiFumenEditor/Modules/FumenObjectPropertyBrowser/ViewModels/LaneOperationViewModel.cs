using Caliburn.Micro;
using Gemini.Modules.Toolbox;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects.Beam;
using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Base.OngekiObjects.Lane;
using OngekiFumenEditor.Base.OngekiObjects.Wall;
using OngekiFumenEditor.Modules.FumenVisualEditor.Base;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels.OngekiObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace OngekiFumenEditor.Modules.FumenObjectPropertyBrowser.ViewModels
{
    public class LaneOperationViewModel : ConnectableObjectOperationViewModel
    {
        public char LaneChar => ConnectableObject.IDShortName[1];

        public LaneOperationViewModel(ConnectableObjectBase obj) : base(obj)
        {

        }

        public override ConnectableChildObjectBase GenerateChildObject(bool needNext)
        {
            return LaneChar switch
            {
                'L' => needNext ? new LaneLeftNext() : new LaneLeftEnd(),
                'C' => needNext ? new LaneCenterNext() : new LaneCenterEnd(),
                'R' => needNext ? new LaneRightNext() : new LaneRightEnd(),
                _ => default
            };
        }

        public override DisplayObjectViewModelBase GenerateChildObjectViewModel(bool needNext)
        {
            return LaneChar switch
            {
                'L' => needNext ? new LaneNextViewModel<LaneLeftNext>() : new LaneEndViewModel<LaneLeftEnd>(),
                'C' => needNext ? new LaneNextViewModel<LaneCenterNext>() : new LaneEndViewModel<LaneCenterEnd>(),
                'R' => needNext ? new LaneNextViewModel<LaneRightNext>() : new LaneEndViewModel<LaneRightEnd>(),
                _ => default
            };
        }
    }
}
