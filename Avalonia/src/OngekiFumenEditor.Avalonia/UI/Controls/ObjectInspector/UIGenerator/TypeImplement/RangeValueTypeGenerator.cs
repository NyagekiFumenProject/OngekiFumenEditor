using Avalonia.Controls;
using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.UI.Controls.ObjectInspector.ViewModels;
using OngekiFumenEditor.Avalonia.Utils;

namespace OngekiFumenEditor.Avalonia.UI.Controls.ObjectInspector.UIGenerator.TypeImplement;

[RegisterTransient<ITypeUIGenerator>]
public class RangeValueTypeGenerator : ITypeUIGenerator
{
    public IEnumerable<Type> SupportTypes { get; } = [typeof(Base.RangeValue)];

    public Control Generate(IObjectPropertyAccessProxy wrapper)
        => ViewHelper.CreateViewByViewModelType(() => new RangeValueTypeUIViewModel(wrapper));
}
