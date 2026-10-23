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
            foreach (var propertyGenerator in IoC.GetAll<IPropertyUIGenerator>())
            {
                try
                {
                    if (propertyGenerator.CanGenerate(wrapper))
                    {
                        var propertyElement = propertyGenerator.Generate(wrapper);
                        wrapper.PropertyChanged += (s, e) =>
                        {
                            propertyElement.IsEnabled = !wrapper.IsReadOnly;
                        };
                        propertyElement.IsEnabled = !wrapper.IsReadOnly;
                        return propertyElement;
                    }
                }
                catch
                {
                    // 与类型生成器保持一致：单个生成器失败不应阻断属性面板。
                }
            }

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
