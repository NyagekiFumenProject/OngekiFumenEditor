using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace OngekiFumenEditor.UI.Controls.ObjectInspector.UIGenerator
{
    public interface ITypeUIGenerator
    {
        public IEnumerable<Type> SupportTypes { get; }
        public UIElement Generate(IObjectPropertyAccessProxy wrapper);
    }
}
