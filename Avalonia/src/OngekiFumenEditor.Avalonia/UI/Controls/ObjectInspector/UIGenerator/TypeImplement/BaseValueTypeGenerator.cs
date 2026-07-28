using Avalonia.Controls;
using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.UI.Controls.ObjectInspector.UIGenerator;
using OngekiFumenEditor.Avalonia.UI.Controls.ObjectInspector.ViewModels;
using OngekiFumenEditor.Avalonia.Utils;

namespace OngekiFumenEditor.Avalonia.UI.Controls.ObjectInspector.UIGenerator.TypeImplement;

[RegisterTransient<ITypeUIGenerator>]
public class BaseValueTypeGenerator : ITypeUIGenerator
{
    public IEnumerable<Type> SupportTypes { get; } =
    [
        typeof(int),
        typeof(long),
        typeof(short),
        typeof(uint),
        typeof(ulong),
        typeof(ushort),
        typeof(string),
        typeof(float),
        typeof(double),
        typeof(int?),
        typeof(long?),
        typeof(short?),
        typeof(uint?),
        typeof(ulong?),
        typeof(ushort?),
        typeof(float?),
        typeof(double?)
    ];

    public Control Generate(IObjectPropertyAccessProxy wrapper)
        => ViewHelper.CreateViewByViewModelType(() => new BaseValueTypeUIViewModel(wrapper));
}
