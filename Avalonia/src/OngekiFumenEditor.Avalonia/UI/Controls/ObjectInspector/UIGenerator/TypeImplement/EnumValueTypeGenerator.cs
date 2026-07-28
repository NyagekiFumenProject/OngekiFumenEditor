using Avalonia.Controls;
using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.UI.Controls.ObjectInspector.ViewModels;
using OngekiFumenEditor.Avalonia.Utils;

namespace OngekiFumenEditor.Avalonia.UI.Controls.ObjectInspector.UIGenerator.TypeImplement;

[RegisterTransient<ITypeUIGenerator>]
public class EnumValueTypeGenerator : ITypeUIGenerator
{
    public IEnumerable<Type> SupportTypes { get; } = [typeof(Enum)];

    public Control Generate(IObjectPropertyAccessProxy wrapper)
        => ViewHelper.CreateViewByViewModelType(() => new EnumValueTypeUIViewModel(wrapper));
}
