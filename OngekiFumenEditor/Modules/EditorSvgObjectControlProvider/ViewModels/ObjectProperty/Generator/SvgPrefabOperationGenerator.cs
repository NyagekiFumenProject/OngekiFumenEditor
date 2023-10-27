using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.EditorObjects.Svg;
using OngekiFumenEditor.Modules.EditorSvgObjectControlProvider.ViewModels.ObjectProperty.Operation;
using OngekiFumenEditor.UI.Controls.ObjectInspector.UIGenerator;
using OngekiFumenEditor.Utils;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Windows;

namespace OngekiFumenEditor.Modules.EditorSvgObjectControlProvider.ViewModels.ObjectProperty.Generator
{
	[Export(typeof(IOngekiObjectOperationGenerator))]
	public class SvgPrefabOperationGenerator : IOngekiObjectOperationGenerator
	{
		public IEnumerable<Type> SupportOngekiTypes { get; } = new[] {
			typeof(SvgPrefabBase)
		};

		public UIElement Generate(OngekiObjectBase obj)
		{
			return ViewHelper.CreateViewByViewModelType(() => new SvgPrefabOperationViewModel(obj as SvgPrefabBase));
		}
	}
}
