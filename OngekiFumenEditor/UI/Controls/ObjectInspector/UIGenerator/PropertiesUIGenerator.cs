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
			var editable =
				wrapper.PropertyInfo.CanWrite &&
				wrapper.PropertyInfo.GetCustomAttribute<ObjectPropertyBrowserReadOnly>() is null;

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
