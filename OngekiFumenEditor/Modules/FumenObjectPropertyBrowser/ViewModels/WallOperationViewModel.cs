using Caliburn.Micro;
using Gemini.Modules.Toolbox;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects.Beam;
using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;
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
    public class WallOperationViewModel : ConnectableObjectOperationViewModel
    {
        public bool IsLeftWall => ConnectableObject.IDShortName[1] == 'L';

        public WallOperationViewModel(ConnectableObjectBase obj) : base(obj)
        {

        }

        public override ConnectableChildObjectBase GenerateChildObject(bool needNext)
        {
            return needNext ? (IsLeftWall ? new WallLeftNext() : new WallRightNext()) : (IsLeftWall ? new WallLeftEnd() : new WallRightEnd());
        }
    }
}
