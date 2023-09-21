using OngekiFumenEditor.Base;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace OngekiFumenEditor.UI.Controls.ObjectInspector.UIGenerator
{
    public interface IOngekiMultiObjectsOperationGenerator
    {
        public bool TryGenerate(IEnumerable<OngekiObjectBase> obj, out UIElement uiElement);
    }
}
