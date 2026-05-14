using OngekiFumenEditor.Core.Base;
using OngekiFumenEditor.Core.Base.OngekiObjects.Beam;
using OngekiFumenEditor.Core.Base.OngekiObjects.ConnectableObject;
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
	public class BeamOperationGenerator : IOngekiObjectOperationGenerator
	{
		public IEnumerable<Type> SupportOngekiTypes { get; } = new[] {
			typeof(BeamStart),
			typeof(BeamNext),
		};

		public UIElement Generate(OngekiObjectBase obj)
		{
			return ViewHelper.CreateViewByViewModelType(() => new BeamOperationViewModel(obj as ConnectableObjectBase));
		}
	}
}
