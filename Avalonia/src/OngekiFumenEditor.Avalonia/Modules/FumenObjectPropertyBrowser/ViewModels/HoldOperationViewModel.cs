using Gekimini.Avalonia.Modules.Toolbox;
using Gekimini.Avalonia.Framework.DragDrops;
using Gekimini.Avalonia.Framework.DragDrops.Behaviors;
using CommunityToolkit.Mvvm.ComponentModel;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Base.DropActions;
using System;
using Avalonia;
using Avalonia.Input;

namespace OngekiFumenEditor.Avalonia.Modules.FumenObjectPropertyBrowser.ViewModels
{
	public class HoldOperationViewModel : ObservableObject
	{
		private bool _draggingItem;
		private Point _mouseStartPosition;

		private Hold connectableObject;
		public Hold ConnectableObject
		{
			get
			{
				return connectableObject;
			}
			set
			{
				SetProperty(ref connectableObject, value);
				CheckEnableDrag();
			}
		}

		private bool isEnableDrag = true;
		public bool IsEnableDrag
		{
			get
			{
				return isEnableDrag;
			}
			set
			{
				SetProperty(ref isEnableDrag, value);
			}
		}

		private void CheckEnableDrag()
		{
			IsEnableDrag = !(ConnectableObject.HoldEnd is not null);
		}

		public HoldOperationViewModel(Hold obj)
		{
			ConnectableObject = obj;
		}

		public void Border_MouseMove2(ActionExecutionContext e)
		{
			if (!_draggingItem)
				return;

			var arg = e.EventArgs as PointerEventArgs;

			Point mousePosition = arg.GetPosition(null);
			Vector diff = _mouseStartPosition - mousePosition;

			if (arg.Properties.IsLeftButtonPressed &&
				(Math.Abs(diff.X) > DragDataContextOutBehavior.MinimumHorizontalDragDistance ||
				Math.Abs(diff.Y) > DragDataContextOutBehavior.MinimumVerticalDragDistance))
			{
				var dropParam = new OngekiObjectDropParam(() =>
				{
					var genWallChild = new HoldEnd();
					ConnectableObject.SetHoldEnd(genWallChild);
					CheckEnableDrag();
					return genWallChild;
				});
				_ = IoC.Get<IDragDropManager>().StartDragDropEvent(arg, dropParam, DragDropEffects.Move);
				_draggingItem = false;
			}
		}

		public void Border_MouseLeftButtonDown(ActionExecutionContext e)
		{
			var arg = e.EventArgs as PointerEventArgs;

			if (!arg.Properties.IsLeftButtonPressed)
				return;

			_mouseStartPosition = arg.GetPosition(null);
			_draggingItem = true;
		}
	}
}


