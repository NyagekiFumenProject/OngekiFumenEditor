using Avalonia.Controls;

namespace OngekiFumenEditor.Avalonia.UI.Controls.ObjectInspector.UIGenerator;

public interface ITypeUIGenerator
{
    IEnumerable<Type> SupportTypes { get; }
    Control Generate(IObjectPropertyAccessProxy wrapper);
}
