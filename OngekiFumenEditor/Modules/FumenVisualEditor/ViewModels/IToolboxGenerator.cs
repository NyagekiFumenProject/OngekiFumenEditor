using OngekiFumenEditor.Base;
using OngekiFumenEditor.Modules.FumenVisualEditor.Base.DropActions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels
{
    public abstract class ToolboxGenerator
    {
        public abstract OngekiObjectBase CreateDisplayObject();
    }

    public class ToolboxGenerator<T> : ToolboxGenerator where T : OngekiObjectBase, new()
    {
        public override OngekiObjectBase CreateDisplayObject()
        {
            return new T();
        }
    }
}
