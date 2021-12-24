using Caliburn.Micro;
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
        public static UIElement GenerateUI(PropertyInfo property, object instance)
        {
            var typeGenerators = IoC.GetAll<ITypeUIGenerator>();
            return typeGenerators
                .Where(x => x.SupportTypes.Contains(property.PropertyType))
                .Select(x=> {
                try
                {
                    return x.Generate(property, instance);
                }
                catch (Exception e)
                {
                    //todo
                    throw;
                }
            }).OfType<UIElement>().FirstOrDefault();
        }
    }
}
