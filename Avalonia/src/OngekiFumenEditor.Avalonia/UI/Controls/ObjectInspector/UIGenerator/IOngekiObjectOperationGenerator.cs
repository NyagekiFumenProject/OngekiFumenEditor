using OngekiFumenEditor.Avalonia.Base;
using System;
using System.Collections.Generic;
using Avalonia;

namespace OngekiFumenEditor.Avalonia.UI.Controls.ObjectInspector.UIGenerator
{
	public interface IOngekiObjectOperationGenerator
	{
		public IEnumerable<Type> SupportOngekiTypes { get; }
		public UIElement Generate(OngekiObjectBase obj);
	}
}

