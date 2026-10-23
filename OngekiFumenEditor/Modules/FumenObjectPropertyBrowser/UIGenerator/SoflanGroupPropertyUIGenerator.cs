using OngekiFumenEditor.Base;
using OngekiFumenEditor.Modules.FumenObjectPropertyBrowser.ViewModels;
using OngekiFumenEditor.UI.Controls.ObjectInspector.UIGenerator;
using OngekiFumenEditor.Utils;
using System;
using System.ComponentModel.Composition;
using System.Windows;

namespace OngekiFumenEditor.Modules.FumenObjectPropertyBrowser.UIGenerator
{
    /// <summary>Soflan / Keyframe Soflan / ISA / ISA 结束端点的 SoflanGroup 属性改用组选择对话框。</summary>
    [Export(typeof(IPropertyUIGenerator))]
    public sealed class SoflanGroupPropertyUIGenerator : IPropertyUIGenerator
    {
        public bool CanGenerate(IObjectPropertyAccessProxy wrapper)
            => wrapper?.PropertyInfo is { Name: nameof(ISoflan.SoflanGroup), PropertyType: var type }
               && type == typeof(int);

        public UIElement Generate(IObjectPropertyAccessProxy wrapper)
            => ViewHelper.CreateViewByViewModelType(() => new SoflanGroupTypeUIViewModel(wrapper));
    }
}
