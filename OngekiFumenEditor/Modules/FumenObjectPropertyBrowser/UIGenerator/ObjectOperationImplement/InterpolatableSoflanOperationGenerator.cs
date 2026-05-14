using OngekiFumenEditor.Core.Base;
using OngekiFumenEditor.Core.Base.EditorObjects;
using OngekiFumenEditor.Core.Base.OngekiObjects;
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
	public class InterpolatableSoflanOperationGenerator : IOngekiObjectOperationGenerator
	{
		public IEnumerable<Type> SupportOngekiTypes { get; } = new[] {
			typeof(InterpolatableSoflan),
		};

		public UIElement Generate(OngekiObjectBase obj)
		{
			return ViewHelper.CreateViewByViewModelType(() => new InterpolatableSoflanOperationViewModel(obj as InterpolatableSoflan));
		}
	}
}
