using Caliburn.Micro;
using OngekiFumenEditor.Base.Attributes;
using System;
using System.Linq;
using System.Reflection;
using System.Windows;

namespace OngekiFumenEditor.UI.Controls.ObjectInspector.UIGenerator
{
    public class PropertiesUIGenerator
    {
        public static UIElement GenerateUI(IObjectPropertyAccessProxy wrapper)
        {
            var typeGenerators = IoC.GetAll<ITypeUIGenerator>();
            var generator = typeGenerators
                .Where(x =>
                    x.SupportTypes.Contains(wrapper.PropertyInfo.PropertyType) ||
                    x.SupportTypes.Any(x => wrapper.PropertyInfo.PropertyType.IsSubclassOf(x))
                    );


            return generator.Select(x =>
                {
                    try
                    {
                        var element = x.Generate(wrapper);
                        wrapper.PropertyChanged += (s, e) =>
                        {
                            //if (e.PropertyName == nameof(IObjectPropertyAccessProxy.ProxyValue))
                            element.IsEnabled = !wrapper.IsReadOnly;
                        };
                        element.IsEnabled = !wrapper.IsReadOnly;
                        return element;
                    }
                    catch
                    {
                        return default;
                    }
                }).OfType<UIElement>().FirstOrDefault();
        }
    }
}
