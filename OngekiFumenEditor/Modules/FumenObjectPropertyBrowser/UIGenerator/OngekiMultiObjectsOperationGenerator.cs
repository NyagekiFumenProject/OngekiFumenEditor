using Caliburn.Micro;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.UI.Controls.ObjectInspector.UIGenerator;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace OngekiFumenEditor.Modules.FumenObjectPropertyBrowser.UIGenerator
{
	public class OngekiMultiObjectsOperationGenerator
	{
		public static UIElement GenerateUI(IEnumerable<OngekiObjectBase> objs)
		{
			var typeGenerators = IoC.GetAll<IOngekiMultiObjectsOperationGenerator>();
			return typeGenerators
				.Select(x =>
				{
					try
					{
						if (x.TryGenerate(objs, out var uiElement))
							return uiElement;
						return default;
					}
					catch
					{
						return default;
					}
				}).OfType<UIElement>().FirstOrDefault();
		}
	}
}
