using OngekiFumenEditor.Modules.FumenObjectPropertyBrowser.ViewModels;
using OngekiFumenEditor.Utils;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace OngekiFumenEditor.Modules.FumenObjectPropertyBrowser.UIGenerator.TypeImplement
{
    [Export(typeof(ITypeUIGenerator))]
    public class BaseValueTypeGenerator : ITypeUIGenerator
    {
        public IEnumerable<Type> SupportTypes { get; } = new[] {
            typeof(int),
            typeof(long),
            typeof(short),

            typeof(uint),
            typeof(ulong),
            typeof(ushort),

            typeof(string),
            typeof(float),
            typeof(double),
        };

        public UIElement Generate(PropertyInfo property, object instance)
        {
            var view = ViewHelper.CreateViewByViewModelType(() => new BaseValueTypeUIViewModel(new()
            {
                OwnerObject = instance,
                PropertyInfo = property,
            }));

            return view;
        }
    }
}
