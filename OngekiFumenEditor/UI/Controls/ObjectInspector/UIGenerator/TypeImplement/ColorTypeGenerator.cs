using OngekiFumenEditor.UI.Controls.ObjectInspector.ViewModels;
using OngekiFumenEditor.Utils;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Drawing;
using System.Windows;

namespace OngekiFumenEditor.UI.Controls.ObjectInspector.UIGenerator.TypeImplement
{
    [Export(typeof(ITypeUIGenerator))]
    public class ColorTypeGenerator : ITypeUIGenerator
    {
        public IEnumerable<Type> SupportTypes { get; } = new[] {
            typeof(Color)
        };

        public UIElement Generate(IObjectPropertyAccessProxy wrapper) => ViewHelper.CreateViewByViewModelType(() => new ColorTypeUIViewModel(wrapper));
    }
}
