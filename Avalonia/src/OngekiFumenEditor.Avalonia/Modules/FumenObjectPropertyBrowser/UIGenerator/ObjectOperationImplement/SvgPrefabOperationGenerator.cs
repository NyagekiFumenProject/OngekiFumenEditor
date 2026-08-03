using Avalonia;
using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Base;
using OngekiFumenEditor.Avalonia.Base.EditorObjects.Svg;
using OngekiFumenEditor.Avalonia.Modules.FumenObjectPropertyBrowser.ViewModels;
using OngekiFumenEditor.Avalonia.Modules.FumenObjectPropertyBrowser.Views;
using OngekiFumenEditor.Avalonia.UI.Controls.ObjectInspector.UIGenerator;

namespace OngekiFumenEditor.Avalonia.Modules.FumenObjectPropertyBrowser.UIGenerator.ObjectOperationImplement;

[RegisterSingleton<IOngekiObjectOperationGenerator>]
public sealed class SvgPrefabOperationGenerator : IOngekiObjectOperationGenerator
{
    public IEnumerable<Type> SupportOngekiTypes { get; } = [typeof(SvgPrefabBase)];

    public UIElement Generate(OngekiObjectBase obj)
    {
        if (obj is not SvgPrefabBase svgPrefab)
            throw new ArgumentException("The operation generator only supports SVG prefabs.", nameof(obj));

        return new SvgPrefabOperationView
        {
            DataContext = new SvgPrefabOperationViewModel(svgPrefab)
        };
    }
}
