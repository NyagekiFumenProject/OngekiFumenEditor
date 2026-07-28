using OngekiFumenEditor.Avalonia.Base;
using OngekiFumenEditor.Avalonia.Base.EditorObjects.Svg;
using OngekiFumenEditor.Avalonia.Modules.EditorSvgObjectControlProvider.ViewModels.ObjectProperty.Operation;
using OngekiFumenEditor.Avalonia.UI.Controls.ObjectInspector.UIGenerator;
using OngekiFumenEditor.Avalonia.Utils;
using System;
using System.Collections.Generic;
using Avalonia;

namespace OngekiFumenEditor.Avalonia.Modules.EditorSvgObjectControlProvider.ViewModels.ObjectProperty.Generator
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


