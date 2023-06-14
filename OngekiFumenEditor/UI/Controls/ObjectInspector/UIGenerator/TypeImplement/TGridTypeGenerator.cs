using OngekiFumenEditor.Base;
using OngekiFumenEditor.UI.Controls.ObjectInspector.ViewModels;
using OngekiFumenEditor.Utils;
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
    public class TGridTypeGenerator : ITypeUIGenerator
    {
        public IEnumerable<Type> SupportTypes { get; } = new[] {
            typeof(TGrid)
        };

        public UIElement Generate(PropertyInfoWrapper wrapper) => ViewHelper.CreateViewByViewModelType(() => new TGridTypeUIViewModel(wrapper));
    }
}
