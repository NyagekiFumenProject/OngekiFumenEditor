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
    [ToolboxItem(typeof(FumenVisualEditorViewModel), "WallStart", "Ongeki Objects")]
    public class WallStartViewModel : DisplayObjectViewModelBase<WallStart>
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
}
