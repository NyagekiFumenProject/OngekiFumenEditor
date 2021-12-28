using OngekiFumenEditor.Base;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace OngekiFumenEditor.Modules.FumenObjectPropertyBrowser.UIGenerator
{
    public interface IOngekiObjectOperationGenerator
    {
        public IEnumerable<Type> SupportOngekiTypes { get; }
        public UIElement Generate(OngekiObjectBase obj);
    }
}
