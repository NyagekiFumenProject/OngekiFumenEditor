using Avalonia.Controls;
using OngekiFumenEditor.Avalonia.Utils;

namespace OngekiFumenEditor.Avalonia.UI.Controls.ObjectInspector.UIGenerator;

public class PropertiesUIGenerator
{
    public static Control GenerateUI(IObjectPropertyAccessProxy wrapper)
    {
        var typeGenerators = IoC.GetAll<ITypeUIGenerator>();
        var generator = typeGenerators
            .Where(x =>
                x.SupportTypes.Contains(wrapper.PropertyInfo.PropertyType) ||
                x.SupportTypes.Any(t => wrapper.PropertyInfo.PropertyType.IsSubclassOf(t)));

        return generator.Select(x =>
            {
                try
                {
                    var element = x.Generate(wrapper);
                    wrapper.PropertyChanged += (_, __) => { element.IsEnabled = !wrapper.IsReadOnly; };
                    element.IsEnabled = !wrapper.IsReadOnly;
                    return element;
                }
                catch
                {
                    return default;
                }
            })
            .OfType<Control>()
            .FirstOrDefault();
    }
}
