using OngekiFumenEditor.Avalonia.Base;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Base;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Base.DropActions;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.ViewModels;
using OngekiFumenEditor.Avalonia.Assets.Languages;
using OngekiFumenEditor.Avalonia.Utils;
using System;
using System.Linq;
using Avalonia;
using Avalonia.Input;

namespace OngekiFumenEditor.Avalonia.Modules.FumenObjectPropertyBrowser.ViewModels.DropActions
{
	public class ConnectableObjectDropAction : IEditorDropHandler
	{
		private readonly ConnectableStartObject startObject;
		private readonly OngekiObjectBase childObject;
		private readonly Action callback;

		public ConnectableObjectDropAction(ConnectableStartObject startObject, ConnectableChildObjectBase childObject, Action callback = default)
		{
			this.startObject = startObject;
			this.childObject = childObject/*CacheLambdaActivator.CreateInstance(childObject.GetType()) as OngekiObjectBase*/;
			this.callback = callback;
		}

		public void Drop(FumenVisualEditorViewModel editor, Point dragEndPoint)
		{

            if (!editor.CheckAndNotifyIfPlaceBeyondDuration(dragEndPoint))
                return;

            var dragTGrid = TGridCalculator.ConvertYToTGrid_DesignMode(dragEndPoint.Y, editor);
			var lastObj = startObject.Children.LastOrDefault();
			var isAppend = Keyboard.IsKeyDown(Key.LeftAlt) || (lastObj is not null && lastObj.TGrid < dragTGrid);
			var isFirst = true;

			editor.UndoRedoManager.ExecuteAction(LambdaUndoAction.Create(Lang.B.AddConnectableNextObject.ToLocalizedString(), () =>
			{
				if (isAppend)
					startObject.AddChildObject(childObject as ConnectableChildObjectBase);
				else
					startObject.InsertChildObject(dragTGrid, childObject as ConnectableChildObjectBase);
				editor.MoveObjectTo(childObject, dragEndPoint);
				if (isFirst)
				{
					editor.NotifyObjectClicked(childObject);
					isFirst = false;
				}
				callback?.Invoke();
			}, () =>
			{
				//startObject.RemoveChildObject(childViewModel as ConnectableChildObjectBase);
				editor.RemoveObject(childObject);
				callback?.Invoke();
			}));
		}
	}
}



