using Caliburn.Micro;
using OngekiFumenEditor.Base;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace OngekiFumenEditor.Modules.FumenObjectPropertyBrowser.UIGenerator
{
    public class OngekiObjectOperationGenerator
    {
        public static UIElement GenerateUI(OngekiObjectBase obj)
        {
            var type = obj.GetType();

            var typeGenerators = IoC.GetAll<IOngekiObjectOperationGenerator>();
            return typeGenerators
                .Where(x =>
                    x.SupportOngekiTypes.Contains(type) ||
                    x.SupportOngekiTypes.Any(x => type.IsSubclassOf(x))
                    )
                .Select(x =>
                {
                    try
                    {
                        return x.Generate(obj);
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
