using OngekiFumenEditor.Avalonia.Base;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects.Lane.Base;
using OngekiFumenEditor.Avalonia.Modules.FumenObjectPropertyBrowser.ViewModels;
using OngekiFumenEditor.Avalonia.UI.Controls.ObjectInspector.UIGenerator;
using OngekiFumenEditor.Avalonia.Utils;
using Injectio.Attributes;
using System.Collections.Generic;
using System.Linq;
using Avalonia;

namespace OngekiFumenEditor.Avalonia.Modules.FumenObjectPropertyBrowser.UIGenerator.ObjectsOperationImplement
{
	[RegisterSingleton<IOngekiMultiObjectsOperationGenerator>]
	public class MultiLanesStartOperationGenerator : IOngekiMultiObjectsOperationGenerator
	{
		public UIElement Generate(OngekiObjectBase obj)
		{
			return ViewHelper.CreateViewByViewModelType(() => new LaneOperationViewModel(obj as ConnectableObjectBase));
		}

		public bool TryGenerate(IEnumerable<OngekiObjectBase> objs, out UIElement uiElement)
		{
			uiElement = default;
			if (!objs.AtCount(2))
				return false;

			var a = objs.First() as ConnectableObjectBase;
			var b = objs.Last() as ConnectableObjectBase;

			if (!((a is ConnectableChildObjectBase && b is LaneStartBase) || (b is ConnectableChildObjectBase && a is LaneStartBase)))
				return false;

			var next = a is LaneStartBase _a ? _a : b as LaneStartBase;
			var prev = a is ConnectableChildObjectBase _a2 ? _a2 : b as ConnectableChildObjectBase;

			if (next.LaneType != (prev.ReferenceStartObject as LaneStartBase)?.LaneType)
				return false;

			uiElement = ViewHelper.CreateViewByViewModelType(() => new MultiLanesOperationViewModel(prev, next));
			return true;
		}
	}
}


