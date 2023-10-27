using OngekiFumenEditor.Base;
using System;
using System.Collections.Generic;
using System.Windows;

namespace OngekiFumenEditor.UI.Controls.ObjectInspector.UIGenerator
{
	public interface IOngekiObjectOperationGenerator
	{
		public IEnumerable<Type> SupportOngekiTypes { get; }
		public UIElement Generate(OngekiObjectBase obj);
	}
}
