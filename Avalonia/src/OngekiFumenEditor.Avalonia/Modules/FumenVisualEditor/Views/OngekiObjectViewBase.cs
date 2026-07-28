using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Views;

public partial class OngekiObjectViewBase : UserControl
{
    private static readonly DropShadowEffect SelectEffect = new()
    {
        ShadowDepth = 0,
        Color = Colors.Yellow,
        BlurRadius = 25
    };

    private static readonly FuncValueConverter<bool, IEffect> IsSelectConverter = new(selected =>
        selected ? SelectEffect : null);

    public OngekiObjectViewBase()
    {
        this.Bind(EffectProperty, new Binding("ReferenceOngekiObject.IsSelected")
        {
            Converter = IsSelectConverter
        });
    }
}
