using Avalonia.Controls;
using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects;
using OngekiFumenEditor.Avalonia.UI.Controls.ObjectInspector.ViewModels;
using OngekiFumenEditor.Avalonia.Utils;

namespace OngekiFumenEditor.Avalonia.UI.Controls.ObjectInspector.UIGenerator.TypeImplement;

[RegisterTransient<ITypeUIGenerator>]
public class BulletPalleteTypeGenerator : ITypeUIGenerator
{
    public IEnumerable<Type> SupportTypes { get; } = [typeof(BulletPallete)];

    public Control Generate(IObjectPropertyAccessProxy wrapper)
        => ViewHelper.CreateViewByViewModelType(() => new BulletPalleteTypeUIViewModel(wrapper));
}
