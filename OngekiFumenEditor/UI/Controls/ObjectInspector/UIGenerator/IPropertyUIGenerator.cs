using System.Windows;

namespace OngekiFumenEditor.UI.Controls.ObjectInspector.UIGenerator
{
    /// <summary>
    /// 按属性语义（而不只是 CLR 类型）定制编辑器 UI 的生成器。
    /// 优先级高于 <see cref="ITypeUIGenerator"/>。
    /// </summary>
    public interface IPropertyUIGenerator
    {
        bool CanGenerate(IObjectPropertyAccessProxy wrapper);
        UIElement Generate(IObjectPropertyAccessProxy wrapper);
    }
}
