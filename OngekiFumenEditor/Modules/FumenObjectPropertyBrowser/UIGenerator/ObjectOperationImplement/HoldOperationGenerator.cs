using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Modules.FumenObjectPropertyBrowser.ViewModels;
using OngekiFumenEditor.UI.Controls.ObjectInspector.UIGenerator;
using OngekiFumenEditor.Utils;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Windows;

namespace OngekiFumenEditor.Modules.FumenObjectPropertyBrowser.UIGenerator.ObjectOperationImplement
{
	[Export(typeof(IOngekiObjectOperationGenerator))]
	public class HoldOperationGenerator : IOngekiObjectOperationGenerator
	{
		public IEnumerable<Type> SupportOngekiTypes { get; } = new[] {
			typeof(Hold),
		};

		public UIElement Generate(OngekiObjectBase obj)
		{
			return ViewHelper.CreateViewByViewModelType(() => new HoldOperationViewModel(obj as Hold));
		}
	}
}
