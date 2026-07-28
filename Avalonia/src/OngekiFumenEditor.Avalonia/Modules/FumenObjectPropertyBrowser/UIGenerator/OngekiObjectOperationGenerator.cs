using OngekiFumenEditor.Avalonia.Base;
using OngekiFumenEditor.Avalonia.UI.Controls.ObjectInspector.UIGenerator;
using System;
using System.Linq;
using Avalonia;
using OngekiFumenEditor.Avalonia.Avalonia;

namespace OngekiFumenEditor.Avalonia.Modules.FumenObjectPropertyBrowser.UIGenerator
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
					catch
					{
						return default;
					}
				}).OfType<UIElement>().FirstOrDefault();
		}
	}
}

