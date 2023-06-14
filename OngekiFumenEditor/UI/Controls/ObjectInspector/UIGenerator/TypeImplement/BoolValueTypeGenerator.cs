using OngekiFumenEditor.Modules.FumenObjectPropertyBrowser.ViewModels;
using OngekiFumenEditor.Utils;
using OngekiFumenEditor.UI.Controls.ObjectInspector.ViewModels;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace OngekiFumenEditor.UI.Controls.ObjectInspector.UIGenerator.TypeImplement
{
    [Export(typeof(ITypeUIGenerator))]
    public class BoolValueTypeGenerator : ITypeUIGenerator
    {
        public IEnumerable<Type> SupportTypes { get; } = new[] {
            typeof(bool),
        };

        public UIElement Generate(PropertyInfoWrapper wrapper) => ViewHelper.CreateViewByViewModelType(() => new BoolValueTypeUIViewModel(wrapper));
    }
}
