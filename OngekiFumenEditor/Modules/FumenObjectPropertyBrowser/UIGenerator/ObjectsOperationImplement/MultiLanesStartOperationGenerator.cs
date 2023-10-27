using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Base.OngekiObjects.Lane.Base;
using OngekiFumenEditor.Modules.FumenObjectPropertyBrowser.ViewModels;
using OngekiFumenEditor.UI.Controls.ObjectInspector.UIGenerator;
using OngekiFumenEditor.Utils;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Windows;

namespace OngekiFumenEditor.Modules.FumenObjectPropertyBrowser.UIGenerator.ObjectsOperationImplement
{
	[Export(typeof(IOngekiMultiObjectsOperationGenerator))]
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
