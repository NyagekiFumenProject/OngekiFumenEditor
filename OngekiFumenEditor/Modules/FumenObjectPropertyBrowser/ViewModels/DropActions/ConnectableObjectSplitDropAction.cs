using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Base.OngekiObjects.Lane.Base;
using OngekiFumenEditor.Modules.FumenVisualEditor;
using OngekiFumenEditor.Modules.FumenVisualEditor.Base;
using OngekiFumenEditor.Modules.FumenVisualEditor.Base.DropActions;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using OngekiFumenEditor.Properties;
using OngekiFumenEditor.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace OngekiFumenEditor.Modules.FumenObjectPropertyBrowser.ViewModels.DropActions
{
	public class ConnectableObjectSplitDropAction : IEditorDropHandler
	{
		private readonly ConnectableStartObject startObject;
		private readonly ConnectableStartObject nextStartObject;
		private readonly ConnectableChildObjectBase prevEndObject;
		private readonly Action callback;

		public ConnectableObjectSplitDropAction(ConnectableStartObject startObject, ConnectableChildObjectBase childObject, Action callback = default)
		{
			this.startObject = startObject;
			prevEndObject = CacheLambdaActivator.CreateInstance(childObject.GetType()) as ConnectableChildObjectBase;
			nextStartObject = CacheLambdaActivator.CreateInstance(startObject.GetType()) as ConnectableStartObject;
			this.callback = callback;
		}

		public void Drop(FumenVisualEditorViewModel editor, Point dragEndPoint)
		{
            if (!editor.CheckAndNotifyIfPlaceBeyondDuration(dragEndPoint))
                return;

            var dragTGrid = TGridCalculator.ConvertYToTGrid_DesignMode(dragEndPoint.Y, editor);
			var splitOutChildren = new List<ConnectableChildObjectBase>();
			var affactedObjects = new HashSet<ILaneDockable>();

			editor.UndoRedoManager.ExecuteAction(LambdaUndoAction.Create(Resources.SplitLane, () =>
			{
				//计算出需要被划分出来的后边子物件集合
				splitOutChildren.AddRange(startObject.Children.Where(x => x.TGrid > dragTGrid));
				affactedObjects.AddRange(editor.Fumen.Taps.AsEnumerable<ILaneDockable>()
					.Concat(editor.Fumen.Holds)
					.Where(x => x.ReferenceLaneStart == startObject));

				//被划分的子物件删除出来
				foreach (var item in splitOutChildren)
				{
					startObject.RemoveChildObject(item);
					nextStartObject.InsertChildObject(item.TGrid, item);
				}

				startObject.AddChildObject(prevEndObject);
				editor.Fumen.AddObject(nextStartObject);

				editor.MoveObjectTo(prevEndObject, dragEndPoint);
				editor.MoveObjectTo(nextStartObject, dragEndPoint);

				foreach (var affactedObj in affactedObjects)
				{
					var tGrid = affactedObj.TGrid;
					affactedObj.ReferenceLaneStart = (tGrid >= startObject.MinTGrid && tGrid <= startObject.MaxTGrid ? startObject : nextStartObject) as LaneStartBase;
				}

				callback?.Invoke();
			}, () =>
			{
				editor.RemoveObject(nextStartObject);
				startObject.RemoveChildObject(prevEndObject);

				foreach (var item in splitOutChildren)
				{
					nextStartObject.RemoveChildObject(item);
					startObject.InsertChildObject(item.TGrid, item);
				}

				foreach (var affactedObj in affactedObjects)
				{
					affactedObj.ReferenceLaneStart = startObject as LaneStartBase;
				}

				splitOutChildren.Clear();
				affactedObjects.Clear();
				callback?.Invoke();
			}));
		}
	}
}
