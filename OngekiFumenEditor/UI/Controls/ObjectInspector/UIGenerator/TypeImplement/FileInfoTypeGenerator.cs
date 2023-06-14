using OngekiFumenEditor.Base;
using OngekiFumenEditor.Modules.FumenObjectPropertyBrowser.ViewModels;
using OngekiFumenEditor.UI.Controls.ObjectInspector.ViewModels;
using OngekiFumenEditor.Utils;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace OngekiFumenEditor.UI.Controls.ObjectInspector.UIGenerator.TypeImplement
{
    [Export(typeof(ITypeUIGenerator))]
    public class FileInfoTypeGenerator : ITypeUIGenerator
    {
        public IEnumerable<Type> SupportTypes { get; } = new[] {
            typeof(FileInfo)
        };

        public UIElement Generate(PropertyInfoWrapper wrapper) => ViewHelper.CreateViewByViewModelType(() => new FileInfoTypeUIViewModel(wrapper));
    }
}
