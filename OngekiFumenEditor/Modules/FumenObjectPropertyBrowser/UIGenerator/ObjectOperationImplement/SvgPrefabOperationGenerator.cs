using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.EditorObjects.Svg;
using OngekiFumenEditor.Base.OngekiObjects.Beam;
using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Base.OngekiObjects.Wall;
using OngekiFumenEditor.Base.OngekiObjects.Wall.Base;
using OngekiFumenEditor.Modules.FumenObjectPropertyBrowser.ViewModels;
using OngekiFumenEditor.Utils;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace OngekiFumenEditor.Modules.FumenObjectPropertyBrowser.UIGenerator.ObjectOperationImplement
{
    [Export(typeof(IOngekiObjectOperationGenerator))]
    public class SvgPrefabOperationGenerator : IOngekiObjectOperationGenerator
    {
        public IEnumerable<Type> SupportOngekiTypes { get; } = new[] {
            typeof(SvgPrefab),
        };

        public UIElement Generate(OngekiObjectBase obj)
        {
            return ViewHelper.CreateViewByViewModelType(() => new SvgPrefabOperationViewModel(obj as SvgPrefab));
        }
    }
}
