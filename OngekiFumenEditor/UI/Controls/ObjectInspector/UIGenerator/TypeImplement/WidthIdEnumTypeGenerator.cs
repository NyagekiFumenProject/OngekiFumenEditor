using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.UI.Controls.ObjectInspector.ViewModels;
using OngekiFumenEditor.Utils;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Windows;

namespace OngekiFumenEditor.UI.Controls.ObjectInspector.UIGenerator.TypeImplement
{
	[Export(typeof(ITypeUIGenerator))]
	public class WidthIdEnumTypeGenerator : ITypeUIGenerator
	{
		public IEnumerable<Type> SupportTypes { get; } = new[] {
			typeof(WidthId)
		};

		public UIElement Generate(IObjectPropertyAccessProxy wrapper) => ViewHelper.CreateViewByViewModelType(() => new WidthIdEnumTypeUIViewModel(wrapper));
	}
}
