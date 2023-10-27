using System;
using System.Collections.Generic;
using System.Windows;

namespace OngekiFumenEditor.UI.Controls.ObjectInspector.UIGenerator
{
	public interface ITypeUIGenerator
	{
		public IEnumerable<Type> SupportTypes { get; }
		public UIElement Generate(IObjectPropertyAccessProxy wrapper);
	}
}
