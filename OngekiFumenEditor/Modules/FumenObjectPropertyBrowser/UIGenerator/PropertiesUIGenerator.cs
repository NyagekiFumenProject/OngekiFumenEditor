using Caliburn.Micro;
using OngekiFumenEditor.Base.Attributes;
using OngekiFumenEditor.Modules.FumenObjectPropertyBrowser.Views;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace OngekiFumenEditor.Modules.FumenObjectPropertyBrowser.UIGenerator
{
    public class PropertiesUIGenerator
    {
        public static UIElement GenerateUI(PropertyInfoWrapper wrapper)
        {
            var editable = 
                wrapper.PropertyInfo.CanWrite &&
                wrapper.PropertyInfo.GetCustomAttribute<ObjectPropertyBrowserReadOnly>() is null;

            var typeGenerators = IoC.GetAll<ITypeUIGenerator>();
            return typeGenerators
                .Where(x =>
                    x.SupportTypes.Contains(wrapper.PropertyInfo.PropertyType) ||
                    x.SupportTypes.Any(x => wrapper.PropertyInfo.PropertyType.IsSubclassOf(x))
                    )
                .Select(x =>
                {
                    try
                    {
                        var element = x.Generate(wrapper);
                        element.IsEnabled = editable;
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
