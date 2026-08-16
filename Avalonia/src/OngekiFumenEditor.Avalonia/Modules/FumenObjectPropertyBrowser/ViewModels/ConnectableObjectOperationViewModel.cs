using Gekimini.Avalonia.Modules.Toolbox;
using OngekiFumenEditor.Avalonia.Base;
using OngekiFumenEditor.Avalonia.Base.EditorObjects.LaneCurve;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Avalonia.Modules.FumenObjectPropertyBrowser.ViewModels.Dialog;
using OngekiFumenEditor.Avalonia.Modules.FumenObjectPropertyBrowser.ViewModels.DropActions;
using OngekiFumenEditor.Avalonia.Modules.FumenObjectPropertyBrowser.Views;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Base;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Base.DropActions;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Kernel;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.ViewModels;
using OngekiFumenEditor.Avalonia.Assets.Languages;
using OngekiFumenEditor.Avalonia.Utils;
using OngekiFumenEditor.Avalonia.Utils.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Input;
using Gekimini.Avalonia.ViewModels;
using OngekiFumenEditor.Avalonia;
using Gekimini.Avalonia.Utils.MethodExtensions;
using Gekimini.Avalonia.Platforms.Services.Window;
using Gekimini.Avalonia.Framework.Dialogs;
using CommunityToolkit.Mvvm.Input;

namespace OngekiFumenEditor.Avalonia.Modules.FumenObjectPropertyBrowser.ViewModels
{
	[MapToView(ViewType = typeof(ConnectableObjectOperationView))]
	public abstract partial class ConnectableObjectOperationViewModel : ViewModelBase
	{
		public enum DragActionType
		{
			DropEnd,
			DropNext,
			DropCurvePathControl,
			Split
		}

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
				OnPropertyChanged();
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
            OnPropertyChanged(nameof(IsEnableDragPathControl));
            OnPropertyChanged(nameof(IsStartObject));
		}

		public ConnectableObjectOperationViewModel(ConnectableObjectBase obj)
		{
			ConnectableObject = obj;
		}

		[RelayCommand]
		private void Interpolate()
		{
			if (RefStartObject.Children.IsEmpty())
			{
				_ = IoC.Get<IDialogManager>().ShowMessageDialog(Lang.DisableInterpolateByNoConnectableChildren);
				return;
			}

			var genStarts = RefStartObject.InterpolateCurve(RefStartObject.CurveInterpolaterFactory).ToArray();

			var editor = IoC.Get<IFumenObjectPropertyBrowser>().Editor;
			editor.UndoRedoManager.ExecuteAction(LambdaUndoAction.Create(Lang.B.InterpolateCurve.ToLocalizedString(), () =>
			{
				editor.EditorContext.Fumen.RemoveObject(RefStartObject);
				foreach (var start in genStarts)
					editor.EditorContext.Fumen.AddObject(start);
			}, () =>
			{
				foreach (var start in genStarts)
					editor.EditorContext.Fumen.RemoveObject(start);
				editor.EditorContext.Fumen.AddObject(RefStartObject);
			}));
		}

		public abstract ConnectableChildObjectBase GenerateChildObject(bool needNext);

		public IEditorDropHandler CreateDropAction(DragActionType actionType)
		{
			if (RefStartObject is null)
				return default;

			//ConnectableObjectDropAction
			var genChildLazy = new Lazy<ConnectableChildObjectBase>(() => GenerateChildObject(actionType == DragActionType.DropNext));
			return actionType switch
			{
				DragActionType.DropNext or DragActionType.DropEnd => new ConnectableObjectDropAction(RefStartObject, genChildLazy.Value, () => CheckEnable()),
				DragActionType.Split => new ConnectableObjectSplitDropAction(RefStartObject, genChildLazy.Value, () => CheckEnable()),
				DragActionType.DropCurvePathControl => new AddLaneCurvePathControlDropAction(ConnectableObject as ConnectableChildObjectBase),
				_ => default
			};
		}

		[RelayCommand]
		private Task BrushAlongLaneAsync()
		{
			var editor = IoC.Get<IFumenObjectPropertyBrowser>().Editor;
			return BrushAlongLaneCoreAsync(
				editor,
				IoC.Get<IFumenEditorClipboard>().CurrentCopiedObjects,
				IoC.Get<IWindowManager>(),
				IoC.Get<IDialogManager>());
		}

		internal async Task BrushAlongLaneCoreAsync(
			FumenVisualEditorViewModel editor,
			IReadOnlyCollection<OngekiObjectBase> copiedObjects,
			IWindowManager windowManager,
			IDialogManager dialogManager)
		{
			var fumen = editor.EditorContext.Fumen;

			if (RefStartObject?.IsPathVaild() != true)
			{
				await dialogManager.ShowMessageDialog(Lang.LaneContainInvalidPath);
				return;
			}

			if (copiedObjects.Count > 1)
			{
				await dialogManager.ShowMessageDialog(Lang.DisableUseBrushByMoreObjects);
				return;
			}

			if (!editor.IsDesignMode)
			{
				await dialogManager.ShowMessageDialog(Lang.EditorMustBeDesignMode);
				return;
			}

			if (copiedObjects.Count < 1)
			{
				await dialogManager.ShowMessageDialog(Lang.CopyOneObjectOnceBeforeUsingBrush);
				return;
			}

			var copiedObjectViewModel = copiedObjects.FirstOrDefault();

			var dialog = new BrushTGridRangeDialogViewModel();
			dialog.BeginTGrid = RefStartObject.MinTGrid.CopyNew();
			dialog.EndTGrid = RefStartObject.MaxTGrid.CopyNew();

			var dialogResult = await windowManager.ShowDialogAsync(dialog);
			if (dialogResult != true)
				return;

			var beginTGrid = dialog.BeginTGrid?.CopyNew();
			var endTGrid = dialog.EndTGrid?.CopyNew();
			if (beginTGrid is null || endTGrid is null || beginTGrid > endTGrid)
				return;

			var generatedObjects = new List<OngekiObjectBase>();

			foreach ((var tGrid, _, _, _, _) in TGridCalculator.GetVisbleTimelines_DesignMode(
				fumen.SoflansMap.DefaultSoflanList, //todo check this
				fumen.BpmList,
				fumen.MeterChanges,
				TGridCalculator.ConvertTGridToY_DesignMode(beginTGrid, editor),
				TGridCalculator.ConvertTGridToY_DesignMode(endTGrid, editor),
				0,
				editor.Setting.BeatSplit,
				editor.Setting.VerticalDisplayScale))
			{
				var obj = copiedObjectViewModel?.CopyNew();
				if (obj is null)
				{
					await dialogManager.ShowMessageDialog(Lang.ObjectNotSupportedInBatchMode);
					return;
				}

				var xGrid = RefStartObject.CalulateXGrid(tGrid);

				if (xGrid is null)
					continue;

				if (obj is ITimelineObject timelineObject)
					timelineObject.TGrid = tGrid.CopyNew();
				if (obj is IHorizonPositionObject horizonPositionObject)
					horizonPositionObject.XGrid = xGrid.CopyNew();

				generatedObjects.Add(obj);
			}

			if (generatedObjects.Count == 0)
				return;

			editor.UndoRedoManager.ExecuteAction(LambdaUndoAction.Create(
				Lang.B.ObjectBatchBrush.ToLocalizedString(),
				() => editor.EditorContext.Fumen.AddObjects(generatedObjects),
				() =>
				{
					foreach (var obj in generatedObjects)
						editor.RemoveObject(obj);
				}));
		}

		[RelayCommand]
		private void InterpolatePart()
		{
			var childObj = ConnectableObject as ConnectableChildObjectBase;

			if (!childObj.CheckCurveVaild())
			{
				_ = IoC.Get<IDialogManager>().ShowMessageDialog(Lang.DisableInterpolatePartByInvaild);
				return;
			}

			var from = childObj;
			var to = childObj.ReferenceStartObject.Children.FindNextOrDefault(childObj);

			var genChildren = childObj.InterpolateCurveChildren(childObj.CurveInterpolaterFactory).ToList();

			var prev = childObj.PrevObject;
			genChildren.RemoveAll(x => x.TGrid >= from.TGrid || x.TGrid <= prev.TGrid);

			var editor = IoC.Get<IFumenObjectPropertyBrowser>().Editor;
			var storeBackupControlPoints = new List<LaneCurvePathControlObject>();

			editor.UndoRedoManager.ExecuteAction(LambdaUndoAction.Create(Lang.B.InterpolatePartCurve.ToLocalizedString(), () =>
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


