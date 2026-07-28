using OngekiFumenEditor.Avalonia.Base;
using System.Collections.Generic;
using Avalonia;

namespace OngekiFumenEditor.Avalonia.UI.Controls.ObjectInspector.UIGenerator
{
	public interface IOngekiMultiObjectsOperationGenerator
	{
		public bool TryGenerate(IEnumerable<OngekiObjectBase> obj, out UIElement uiElement);
	}
}

