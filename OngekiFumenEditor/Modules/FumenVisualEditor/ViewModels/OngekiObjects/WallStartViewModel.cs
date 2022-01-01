using Gemini.Modules.Toolbox;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Base.OngekiObjects.Beam;
using OngekiFumenEditor.Base.OngekiObjects.Wall;
using OngekiFumenEditor.Modules.FumenVisualEditor.Views.OngekiObjects;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels.OngekiObjects
{
    public class WallStartViewModel<T> : DisplayObjectViewModelBase<T> where T : WallStart, new()
    {
        public override OngekiObjectBase ReferenceOngekiObject
        {
            get
            {
                return base.ReferenceOngekiObject;
            }
            set
            {
                base.ReferenceOngekiObject = value;
            }
        }
    }

    [ToolboxItem(typeof(FumenVisualEditorViewModel), "WallLeftStart", "Ongeki Objects")]
    public class WallLeftStartViewModel : WallStartViewModel<WallLeftStart>
    {

    }

    [ToolboxItem(typeof(FumenVisualEditorViewModel), "WallRightStart", "Ongeki Objects")]
    public class WallRightStartViewModel : WallStartViewModel<WallRightStart>
    {

    }
}
