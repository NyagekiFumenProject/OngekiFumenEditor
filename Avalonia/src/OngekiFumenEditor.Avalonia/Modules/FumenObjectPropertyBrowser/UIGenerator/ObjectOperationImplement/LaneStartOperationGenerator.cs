using OngekiFumenEditor.Avalonia.Base;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects.Lane.Base;
using OngekiFumenEditor.Avalonia.Modules.FumenObjectPropertyBrowser.ViewModels;
using OngekiFumenEditor.Avalonia.UI.Controls.ObjectInspector.UIGenerator;
using OngekiFumenEditor.Avalonia.Utils;
using System;
using System.Collections.Generic;
using Avalonia;

namespace OngekiFumenEditor.Avalonia.Modules.FumenObjectPropertyBrowser.UIGenerator.ObjectOperationImplement
{
	[Export(typeof(IOngekiObjectOperationGenerator))]
	public class LaneStartOperationGenerator : IOngekiObjectOperationGenerator
	{
		public IEnumerable<Type> SupportOngekiTypes { get; } = new[] {
			typeof(LaneStartBase),
			typeof(LaneNextBase)
		};

		public UIElement Generate(OngekiObjectBase obj)
		{
			return ViewHelper.CreateViewByViewModelType(() => new LaneOperationViewModel(obj as ConnectableObjectBase));
		}
	}
}


