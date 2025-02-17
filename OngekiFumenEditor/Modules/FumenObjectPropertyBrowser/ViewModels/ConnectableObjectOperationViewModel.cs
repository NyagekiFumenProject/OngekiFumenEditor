using Caliburn.Micro;
using Gemini.Modules.Toolbox;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.EditorObjects.LaneCurve;
using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Modules.FumenObjectPropertyBrowser.ViewModels.Dialog;
using OngekiFumenEditor.Modules.FumenObjectPropertyBrowser.ViewModels.DropActions;
using OngekiFumenEditor.Modules.FumenObjectPropertyBrowser.Views;
using OngekiFumenEditor.Modules.FumenVisualEditor;
using OngekiFumenEditor.Modules.FumenVisualEditor.Base;
using OngekiFumenEditor.Modules.FumenVisualEditor.Base.DropActions;
using OngekiFumenEditor.Modules.FumenVisualEditor.Kernel;
using OngekiFumenEditor.Properties;
using OngekiFumenEditor.Utils;
using OngekiFumenEditor.Utils.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;

namespace OngekiFumenEditor.Modules.FumenObjectPropertyBrowser.ViewModels
{
	[MapToView(ViewType = typeof(ConnectableObjectOperationView))]
	public abstract class ConnectableObjectOperationViewModel : PropertyChangedBase
	{
		public enum DragActionType
		{
			DropEnd,
			DropNext,
			DropCurvePathControl,
			Split
		}

		private bool _draggingItem;
		private Point _mouseStartPosition;

		private ConnectableObjectBase connectableObject;
		public ConnectableObjectBase ConnectableObject
		{
			get
			{
				return connectableObject;
			}
			set
			{
				connectableObject = value;
				NotifyOfPropertyChange(() => ConnectableObject);
				CheckEnable();
			}
		}

		public ConnectableStartObject RefStartObject => ConnectableObject switch
		{
			ConnectableStartObject start => start,
			ConnectableChildObjectBase next => next.ReferenceStartObject,
			_ => default,
		};

		public bool IsEnableDragPathControl => ConnectableObject is ConnectableChildObjectBase;
		public bool IsStartObject => ConnectableObject is ConnectableStartObject;

		private void CheckEnable()
		{
			NotifyOfPropertyChange(() => IsEnableDragPathControl);
			NotifyOfPropertyChange(() => IsStartObject);
		}

		public ConnectableObjectOperationViewModel(ConnectableObjectBase obj)
		{
			ConnectableObject = obj;
		}

		public void Border_MouseMove(ActionExecutionContext e)
		{
			ProcessDragStart(e, DragActionType.DropNext);
		}

		public void Border_MouseMove4(ActionExecutionContext e)
		{
			ProcessDragStart(e, DragActionType.DropCurvePathControl);
		}

		public void Border_MouseMove2(ActionExecutionContext e)
		{
			ProcessDragStart(e, DragActionType.DropEnd);
		}

		public void Border_MouseMove3(ActionExecutionContext e)
		{
			ProcessDragStart(e, DragActionType.Split);
		}

		public void Interpolate(ActionExecutionContext e)
		{
			if (RefStartObject.Children.IsEmpty())
			{
				MessageBox.Show(Resources.DisableInterpolateByNoConnectableChildren);
				return;
			}

			var genStarts = RefStartObject.InterpolateCurve(RefStartObject.CurveInterpolaterFactory).ToArray();

			var editor = IoC.Get<IFumenObjectPropertyBrowser>().Editor;
			editor.UndoRedoManager.ExecuteAction(LambdaUndoAction.Create(Resources.InterpolateCurve, () =>
			{
				editor.Fumen.RemoveObject(RefStartObject);
				foreach (var start in genStarts)
					editor.Fumen.AddObject(start);
			}, () =>
			{
				foreach (var start in genStarts)
					editor.Fumen.RemoveObject(start);
				editor.Fumen.AddObject(RefStartObject);
			}));
		}

		public abstract ConnectableChildObjectBase GenerateChildObject(bool needNext);

		private void ProcessDragStart(ActionExecutionContext e, DragActionType actionType)
		{
			if ((!_draggingItem) || RefStartObject is null)
				return;

			var arg = e.EventArgs as MouseEventArgs;

			Point mousePosition = arg.GetPosition(null);
			Vector diff = _mouseStartPosition - mousePosition;

			if (arg.LeftButton == MouseButtonState.Pressed &&
				(Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
				Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance))
			{
				//ConnectableObjectDropAction
				var genChildLazy = new Lazy<ConnectableChildObjectBase>(() => GenerateChildObject(actionType == DragActionType.DropNext));
				IEditorDropHandler dropAction = actionType switch
				{
					DragActionType.DropNext or DragActionType.DropEnd => new ConnectableObjectDropAction(RefStartObject, genChildLazy.Value, () => CheckEnable()),
					DragActionType.Split => new ConnectableObjectSplitDropAction(RefStartObject, genChildLazy.Value, () => CheckEnable()),
					DragActionType.DropCurvePathControl => new AddLaneCurvePathControlDropAction(ConnectableObject as ConnectableChildObjectBase),
					_ => default
				};

				DragDrop.DoDragDrop(e.Source, new DataObject(ToolboxDragDrop.DataFormat, dropAction), DragDropEffects.Move);
				_draggingItem = false;
			}
		}

		public void Border_MouseLeftButtonDown(ActionExecutionContext e)
		{
			var arg = e.EventArgs as MouseEventArgs;

			if (arg.LeftButton != MouseButtonState.Pressed)
				return;

			_mouseStartPosition = arg.GetPosition(null);
			_draggingItem = true;
		}

		public async void OnBrushButtonClick()
		{
			var editor = IoC.Get<IFumenObjectPropertyBrowser>().Editor;
			var fumen = editor.Fumen;

			if (RefStartObject?.IsPathVaild() != true)
			{
				MessageBox.Show(Resources.LaneContainInvalidPath);
				return;
			}

			var copiedObjects = IoC.Get<IFumenEditorClipboard>().CurrentCopiedObjects;

			if (copiedObjects.Count() > 1)
			{
				MessageBox.Show(Resources.DisableUseBrushByMoreObjects);
				return;
			}

			if (!editor.IsDesignMode)
			{
				MessageBox.Show(Resources.EditorMustBeDesignMode);
				return;
			}

			if (copiedObjects.Count() < 1)
			{
				MessageBox.Show(Resources.CopyOneObjectOnceBeforeUsingBrush);
				return;
			}

			var copiedObjectViewModel = copiedObjects.FirstOrDefault();

			if (copiedObjectViewModel?.CopyNew() is null)
			{
				MessageBox.Show(Resources.ObjectNotSupportedInBatchMode);
				return;
			}

			var dialog = new BrushTGridRangeDialogViewModel();
			dialog.BeginTGrid = RefStartObject.MinTGrid.CopyNew();
			dialog.EndTGrid = RefStartObject.MaxTGrid.CopyNew();

			if ((await IoC.Get<IWindowManager>().ShowDialogAsync(dialog)) != true)
				return;

			var beginTGrid = dialog.BeginTGrid;
			var endTGrid = dialog.EndTGrid;

			var redoAction = new System.Action(() => { });
			var undoAction = new System.Action(() => { });

			foreach ((var tGrid, _, _, _, _) in TGridCalculator.GetVisbleTimelines_DesignMode(
				fumen.Soflans,
				fumen.BpmList,
				fumen.MeterChanges,
				TGridCalculator.ConvertTGridToY_DesignMode(beginTGrid, editor),
				TGridCalculator.ConvertTGridToY_DesignMode(endTGrid, editor),
				0,
				editor.Setting.BeatSplit,
				editor.Setting.VerticalDisplayScale))
			{
				var obj = copiedObjectViewModel.CopyNew();
				var xGrid = RefStartObject.CalulateXGrid(tGrid);

				if (xGrid is null)
					continue;

				redoAction += () =>
				{
					if (obj is ITimelineObject timelineObject)
						timelineObject.TGrid = tGrid;
					if (obj is IHorizonPositionObject horizonPositionObject)
						horizonPositionObject.XGrid = xGrid;

					editor.Fumen.AddObject(obj);
				};
				undoAction += () =>
				{
					editor.RemoveObject(obj);
				};
			}


			editor.UndoRedoManager.ExecuteAction(LambdaUndoAction.Create(Resources.ObjectBatchBrush, redoAction, undoAction));
		}

		public void OnPartChildCurveInterpolateClick()
		{
			PartChildCurveInterpolate();
		}

		public void PartChildCurveInterpolate()
		{
			var childObj = ConnectableObject as ConnectableChildObjectBase;

			if (!childObj.CheckCurveVaild())
			{
				MessageBox.Show(Resources.DisableInterpolatePartByInvaild);
				return;
			}

			var from = childObj;
			var to = childObj.ReferenceStartObject.Children.FindNextOrDefault(childObj);

			var genChildren = childObj.InterpolateCurveChildren(childObj.CurveInterpolaterFactory).ToList();

			var prev = childObj.PrevObject;
			genChildren.RemoveAll(x => x.TGrid >= from.TGrid || x.TGrid <= prev.TGrid);

			var editor = IoC.Get<IFumenObjectPropertyBrowser>().Editor;
			var storeBackupControlPoints = new List<LaneCurvePathControlObject>();

			editor.UndoRedoManager.ExecuteAction(LambdaUndoAction.Create(Resources.InterpolatePartCurve, () =>
			{
				foreach (var newChild in genChildren)
					childObj.ReferenceStartObject.InsertChildObject(newChild.TGrid, newChild);

				storeBackupControlPoints.AddRange(childObj.PathControls);
				foreach (var cp in storeBackupControlPoints)
					childObj.RemoveControlObject(cp);

			}, () =>
			{
				foreach (var newChild in genChildren)
					childObj.ReferenceStartObject.RemoveChildObject(newChild);

				foreach (var cp in storeBackupControlPoints)
					childObj.AddControlObject(cp);
				storeBackupControlPoints.Clear();

			}));
		}
	}
}
