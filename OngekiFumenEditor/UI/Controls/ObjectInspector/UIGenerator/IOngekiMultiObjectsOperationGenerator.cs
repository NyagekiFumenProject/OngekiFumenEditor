using OngekiFumenEditor.Base;
using System.Collections.Generic;
using System.Windows;

namespace OngekiFumenEditor.UI.Controls.ObjectInspector.UIGenerator
{
	public interface IOngekiMultiObjectsOperationGenerator
	{
		public bool TryGenerate(IEnumerable<OngekiObjectBase> obj, out UIElement uiElement);
	}
}
