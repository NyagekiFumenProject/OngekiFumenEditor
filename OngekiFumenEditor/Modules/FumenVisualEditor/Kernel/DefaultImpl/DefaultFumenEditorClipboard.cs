#nullable enable
using Caliburn.Micro;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.EditorObjects.LaneCurve;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Base.OngekiObjects.Lane.Base;
using OngekiFumenEditor.Modules.FumenObjectPropertyBrowser;
using OngekiFumenEditor.Modules.FumenVisualEditor.Base;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using OngekiFumenEditor.Properties;
using OngekiFumenEditor.Utils;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Formats.Tar;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms.VisualStyles;
using OngekiFumenEditor.Base.OngekiObjects.Lane;
using static OngekiFumenEditor.Base.OngekiObjects.Flick;
using static OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels.FumenVisualEditorViewModel;
using ILaneDockable = OngekiFumenEditor.Base.ILaneDockable;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Kernel.DefaultImpl
{
	[Export(typeof(IFumenEditorClipboard))]
	[PartCreationPolicy(CreationPolicy.Shared)]
	internal class DefaultFumenEditorClipboard : IFumenEditorClipboard
	{
		private Dictionary<OngekiObjectBase, (XGrid? x, TGrid y)> currentCopiedSources = new();

		private FumenVisualEditorViewModel sourceEditor;
		private double prevScale;

		public bool ContainPastableObjects => sourceEditor is not null && currentCopiedSources.Any();

		public IReadOnlyCollection<OngekiObjectBase> CurrentCopiedObjects => currentCopiedSources.Keys;

		public async Task CopyObjects(FumenVisualEditorViewModel sourceEditor, IEnumerable<ISelectableObject> objects)
		{
			objects = objects.ToList();

			if (sourceEditor.IsLocked)
				return;
			if (sourceEditor.Fumen is null)
				return;
			if (!sourceEditor.IsDesignMode) {
				sourceEditor.ToastNotify(Resources.EditorMustBeDesignMode);
				return;
			}

			if (objects.IsEmpty()) {
				sourceEditor.ToastNotify(Resources.ClearCopyList);
				return;
			}

			prevScale = sourceEditor.Setting.VerticalDisplayScale;

			//清空一下
			currentCopiedSources.Clear();

			// TODO WTF?
			//this.sorceEditor = default;

			Dictionary<OngekiObjectBase, OngekiObjectBase> copiedObjectMap = new();
			Dictionary<ILaneDockable, ConnectableStartObject> sourceDockablesToCopiedLanes = new();
			List<OngekiObjectBase> generatedObjects = new();

			// Process each lane that has connectables in the selection
			foreach (var laneStart in objects.OfType<ConnectableObjectBase>().DistinctBy(c => c.RecordId)
				         .Select(c => c.ReferenceStartObject))
			{
				// All selected nodes in the current lane
				var selectedNodes = laneStart.Children.Prepend<ConnectableObjectBase>(laneStart)
					.Intersect(objects.OfType<ConnectableObjectBase>())
					.ToList();
				var first = selectedNodes.First();

				var start = first.LaneType.CreateStartConnectable();
				start.Copy(first);
				start.RecordId = -Math.Abs(start.RecordId) - 1;

				// By default, make the first node of the lane a StartObject
				// If we auto-generate a node on a dockable before the selected nodes, this gets set to 0
				var skipNodes = 1;

				// Gets set if we auto-generate a node after the selected nodes
				ConnectableChildObjectBase? tail = null;

				// Create new nodes for any dockables outside the selected lane range
				var dockables = objects.OfType<ILaneDockable>()
					.Where(d => laneStart == d.ReferenceLaneStart)
					.OrderBy(o => o.TGrid).ToList();

				if (dockables.Count > 0) {
					if (dockables.First().TGrid < selectedNodes.First().TGrid) {
						// There's a dockable before the selected lanes
						skipNodes = 0;
						start.TGrid = dockables.First().TGrid;
						start.XGrid = dockables.First().XGrid;
						generatedObjects.Add(start);
					}

					if (dockables.Last().TGrid > selectedNodes.Last().TGrid || selectedNodes.Count == 1) {
						// There's a dockable after the selected range
						tail = dockables.Last().ReferenceLaneStart.LaneType.CreateChildConnectable();
						tail.TGrid = dockables.Last().TGrid;
						tail.XGrid = dockables.Last().XGrid;
						generatedObjects.Add(tail);
					}

					foreach (var dockable in dockables) {
						sourceDockablesToCopiedLanes[dockable] = start;
					}
				}

				if (!generatedObjects.Contains(start))
					copiedObjectMap[first] = start;

				// Re-create the lane in the clipboard
				foreach (var selectedNode in selectedNodes.Skip(skipNodes)) {
					var newNode = selectedNode.LaneType.CreateChildConnectable();
					newNode.Copy(selectedNode);
					copiedObjectMap[selectedNode] = newNode;
					start.AddChildObject(newNode);
				}

				if (tail is not null) {
					start.AddChildObject(tail);
				}
			}

			foreach (var dockableGroup in objects.OfType<ILaneDockable>()
				         .Where(d => d.ReferenceLaneStart is not null)
				         .GroupBy(d => d.ReferenceLaneStart)
				         .Where(g => g.Any(d => d is not HoldEnd)))
			{
				if (!sourceDockablesToCopiedLanes.ContainsKey(dockableGroup.First())) {
					// Process selected dockables that have no selected lane nodes
					var head = dockableGroup.Key.LaneType.CreateStartConnectable();
					head.RecordId = -Math.Abs(head.RecordId) - 1;
					head.TGrid = dockableGroup.First().TGrid;
					head.XGrid = dockableGroup.First().XGrid;

					var tail = dockableGroup.Key.LaneType.CreateChildConnectable();
					tail.RecordId = -Math.Abs(head.RecordId) - 1;
					tail.TGrid = dockableGroup.Last().TGrid;
					tail.XGrid = dockableGroup.Last().XGrid;

					head.AddChildObject(tail);

					generatedObjects.Add(head);
					generatedObjects.Add(tail);

					foreach (var sourceDockable in dockableGroup) {
						sourceDockablesToCopiedLanes[sourceDockable] = head;
					}
				}

				foreach (var sourceDockable in dockableGroup.OrderBy(d => d is HoldEnd)) {
					var newDockable = ((OngekiMovableObjectBase)sourceDockable).CopyNew();
//					((ILaneDockable)newDockable).ReferenceLaneStart = (LaneStartBase)sourceDockablesToCopiedLanes[sourceDockable];
					copiedObjectMap[(OngekiObjectBase)sourceDockable] = newDockable;

					if (sourceDockable is HoldEnd sourceHoldEnd) {
						((Hold)copiedObjectMap[sourceHoldEnd.RefHold]).SetHoldEnd((HoldEnd)newDockable);
					}
				}
			}

			// Copy non-lane objects and undocked dockables
			foreach (var obj in objects.Where(x => x switch
			         {
				         HoldEnd end when !objects.Contains(end.RefHold) => false,
				         ConnectableObjectBase => false,
				         LaneCurvePathControlObject => false,
				         LaneBlockArea.LaneBlockAreaEndIndicator => false,
				         Soflan.SoflanEndIndicator => false,
				         _ => true,
			         }))
			{
				if (obj is not OngekiObjectBase source) {
					Log.LogWarn($"Attempted to copy invalid object {obj}");
					continue;
				}

				if (!copiedObjectMap.ContainsKey(source))
					copiedObjectMap[source] = source.CopyNew();
				var copied = copiedObjectMap[source];

				var position = (
					obj is IHorizonPositionObject xPosObject ? xPosObject.XGrid : null,
					((ITimelineObject)obj).TGrid
				);

				switch (copied) {
					case ILaneDockable { ReferenceLaneStart: not null } dockable:
						dockable.ReferenceLaneStart = (LaneStartBase)sourceDockablesToCopiedLanes[(ILaneDockable)source];

						if (dockable is HoldEnd end) {
							((Hold)copiedObjectMap[((HoldEnd)obj).RefHold]).SetHoldEnd(end);
						}
						break;
					//特殊处理LBK:连End物件一起复制了
					case LaneBlockArea laneBlock:
						laneBlock.CopyEntire((LaneBlockArea)source);
						break;
					//特殊处理SFL:连End物件一起复制了
					case Soflan soflan:
						soflan.CopyEntire((Soflan)source);
						break;

					case IBulletPalleteReferencable bulletPalleteObject:
						if (bulletPalleteObject.ReferenceBulletPallete is BulletPallete bpl)
							bulletPalleteObject.ReferenceBulletPallete = bpl;
						break;
				}

				//注册,并记录当前位置
				currentCopiedSources[copied] = position;
			}

			// Put copied objects on the clipboard
			foreach (var copiedObject in generatedObjects.Concat(copiedObjectMap.Values)) {
				var position = (
					copiedObject is IHorizonPositionObject xPosObject ? xPosObject.XGrid : null,
					((ITimelineObject)copiedObject).TGrid
				);
				currentCopiedSources[copiedObject] = position;
			}

			if (currentCopiedSources.Count == 0)
				sourceEditor.ToastNotify(Resources.ClearCopyList);
			else {
				this.sourceEditor = sourceEditor;
				sourceEditor.ToastNotify(currentCopiedSources.Count == 1
					? Resources.CopiedObjectsBrushAllowed.Format(currentCopiedSources.Count)
					: Resources.CopiedObjects.Format(currentCopiedSources.Count));
			}

			return;
		}

		public Task PasteObjects(FumenVisualEditorViewModel targetEditor, PasteOption pasteOption,
			Point? placePoint = null)
		{
			var (gridPlaceX, gridPlaceY) = placePoint is not null
				? (XGridCalculator.ConvertXToXGrid(placePoint.Value.X, targetEditor),
					TGridCalculator.ConvertYToTGrid_DesignMode(placePoint.Value.Y, targetEditor))
				: (XGrid.Zero,
					TGridCalculator.ConvertYToTGrid_DesignMode(targetEditor.Setting.JudgeLineOffsetY, targetEditor));

			var clipboardObjectsRootPosition = (
				currentCopiedSources.Values.MinBy(pos => pos.x ?? XGrid.MaxValue).x,
				currentCopiedSources.Values.MinBy(pos => pos.y).y
			);

			var clipboardDockableLanes =
				currentCopiedSources.Keys.OfType<ILaneDockable>().ToDictionary(d => d, d => d.ReferenceLaneStart);

			// Maps clipboard objects to the new copies that will be added to the fumen
			var clipboardCopyMap = currentCopiedSources.Keys.ToDictionary(o => o, o => o.CopyNew());

			// Set reference lanes for dockables
			foreach (var (dockableSource, dockableCopy) in clipboardCopyMap.Where(kv => kv.Key is ILaneDockable)) {
				((ILaneDockable)dockableCopy).ReferenceLaneStart = clipboardDockableLanes[(ILaneDockable)dockableSource];

				// Set up HoldEnds
				if (dockableSource is HoldEnd sourceHoldEnd) {
					var copyHoldEnd = (HoldEnd)dockableCopy;
					var copyHoldStart = (Hold)clipboardCopyMap[sourceHoldEnd.RefHold];
					copyHoldStart.SetHoldEnd(copyHoldEnd);
				}
			}

			foreach (var (clipboardObject, (xGrid, tGrid)) in currentCopiedSources.OrderBy(kv => kv.Key is ILaneDockable)) {
				var newObject = clipboardCopyMap[clipboardObject];

				var tGridObject = (ITimelineObject)newObject;
				var offsetY = tGridObject.TGrid - clipboardObjectsRootPosition.y;
				var destinationY = gridPlaceY + offsetY;
				((ITimelineObject)newObject).TGrid = destinationY;

				if (xGrid is not null) {
					var offsetX = xGrid - clipboardObjectsRootPosition.x;
					var destinationX = gridPlaceX + offsetX;
					((IHorizonPositionObject)newObject).XGrid = destinationX;
				}
			}

			var previousSelection = targetEditor.Fumen.GetAllDisplayableObjects().OfType<ISelectableObject>()
				.Where(s => s.IsSelected);

			var redo = () =>
			{
				previousSelection.ForEach(o => o.IsSelected = false);

				foreach (var (clipboardObject, newObject) in clipboardCopyMap.Where(kv => kv.Key is ConnectableChildObjectBase)) {
					((LaneStartBase)clipboardCopyMap[((ConnectableObjectBase)clipboardObject).ReferenceStartObject]).AddChildObject((ConnectableChildObjectBase)newObject);
				}

				foreach (var newObject in clipboardCopyMap.Values) {
					targetEditor.Fumen.AddObject(newObject);
					if (newObject is ISelectableObject selectable)
						selectable.IsSelected = true;
				}

				foreach (var (clipboardObject, newObject) in clipboardCopyMap) {
					if (newObject is ILaneDockable newDockable) {
						newDockable.ReferenceLaneStart = (LaneStartBase)clipboardCopyMap[clipboardDockableLanes[(ILaneDockable)clipboardObject]];
					}
				}
			};

			var undo = () =>
			{
				targetEditor.Fumen.GetAllDisplayableObjects().OfType<ISelectableObject>().ForEach(o => o.IsSelected = false);

				foreach (var newObject in clipboardCopyMap.Values) {
					targetEditor.Fumen.RemoveObject(newObject);
				}

				previousSelection.ForEach(o => o.IsSelected = true);
			};

			targetEditor.UndoRedoManager.ExecuteAction(LambdaUndoAction.Create(Resources.CopyAndPaste, redo, undo));

			return Task.CompletedTask;
		}

		/*
		public async Task PasteObjects(FumenVisualEditorViewModel targetEditor, PasteOption pasteOption, Point? placePoint = null)
		{
			if (targetEditor.IsLocked)
				return;
			if (sourceEditor is null)
			{
				Log.LogWarn($"无法粘贴因为sourceEditor为空");
				return;
			}
			if (currentCopiedSources.Count is 0)
			{
				Log.LogWarn($"无法粘贴因为复制列表为空");
				return;
			}
			var curScale = targetEditor.Setting.VerticalDisplayScale;
			if (curScale != prevScale && currentCopiedSources.Count > 1)
			{
				targetEditor.ToastNotify(Resources.CantPasteMoreObjectByScaleDifferent.Format(prevScale, curScale));
				return;
			}

			bool isSameEditorCopy = sourceEditor == targetEditor;
			//convert y form sourceEditor to targetEditor
			double adjustY(double y)
			{
				if (isSameEditorCopy)
					return y;
				var offsetTGrid = TGridCalculator.ConvertYToTGrid_DesignMode(y, sourceEditor);
				var fixedY = TGridCalculator.ConvertTGridToY_DesignMode(offsetTGrid, targetEditor);
				return fixedY;
			}

			double adjustX(double x)
			{
				if (isSameEditorCopy)
					return x;
				var offsetXGrid = XGridCalculator.ConvertXToXGrid(x, sourceEditor);
				var fixedX = XGridCalculator.ConvertXGridToX(offsetXGrid, targetEditor);
				return fixedX;
			}

			//计算出镜像中心位置
			var mirrorYOpt = CalculateYMirror(currentCopiedSources.Keys, pasteOption);
			var mirrorXOpt = CalculateXMirror(targetEditor, currentCopiedSources.Keys, pasteOption);

			//获取源中心点
			var sourceCenterPos = CalculateRangeCenter(currentCopiedSources.Keys);
			var fixedY = adjustY(sourceCenterPos.Y);
			var fixedCenterPos = new Point(sourceCenterPos.X, fixedY);
			//获取目标中心点
			var targetPoint = placePoint ?? fixedCenterPos;
			//计算出偏移量
			var offset = (Point)(targetPoint - fixedCenterPos);

			if (pasteOption == PasteOption.XGridZeroMirror)
				offset.X = 0;

			var redo = new System.Action(() => targetEditor.TryCancelAllObjectSelecting());
			var undo = new System.Action(() => targetEditor.TryCancelAllObjectSelecting());

			foreach (var pair in currentCopiedSources)
			{
				var source = pair.Key;
				var sourceCanvasPos = pair.Value;

				var copied = source.CopyNew();
				if (copied is null)
					continue;

				var posMap = new Dictionary<object, (Point pos, TGrid tGrid, XGrid xGrid)>();
				void updateY(object obj, double y, TGrid tGrid)
				{
					var pos = posMap.TryGetValue(obj, out var p) ? p : default;
					pos.pos.Y = y;
					pos.tGrid = tGrid;
					posMap[obj] = pos;
				}

				void updateX(object obj, double x, XGrid xGrid)
				{
					var pos = posMap.TryGetValue(obj, out var p) ? p : default;
					pos.pos.X = x;
					pos.xGrid = xGrid;
					posMap[obj] = pos;
				}

				switch (copied)
				{
					//特殊处理ConnectableStart:连Child和Control一起复制了,顺便删除RecordId(添加时需要重新分配而已)
					case ConnectableStartObject _start:
						_start.CopyEntireConnectableObject((ConnectableStartObject)source);
						redo += () => _start.RecordId = -1;
						break;
					//特殊处理LBK:连End物件一起复制了
					case LaneBlockArea _lbk:
						_lbk.CopyEntire((LaneBlockArea)source);
						break;
					//特殊处理SFL:连End物件一起复制了
					case Soflan _sfl:
						_sfl.CopyEntire((Soflan)source);
						break;
					//特殊处理Hold:清除Id
					case Hold hold:
						hold.ReferenceLaneStart = default;
						undo += () => hold.ReferenceLaneStart = default;
						break;
					case Flick flick:
						if (pasteOption == PasteOption.XGridZeroMirror
							|| pasteOption == PasteOption.SelectedRangeCenterXGridMirror)
						{
							var beforeDirection = flick.Direction;
							redo += () => flick.Direction = (FlickDirection)(-(int)beforeDirection);
							undo += () => flick.Direction = beforeDirection;
						}
						break;
					default:
						break;
				}

				TGrid newTGrid = default;
				if (copied is ITimelineObject timelineObject)
				{
					var tGrid = timelineObject.TGrid.CopyNew();

					double CalcY(double sourceEditorY)
					{
						if (pasteOption == PasteOption.Direct)
							return sourceEditorY;
						var fixedY = adjustY(sourceEditorY);

						var mirrorBaseY = mirrorYOpt is double _mirrorY ? _mirrorY : fixedY;
						var mirroredY = mirrorBaseY + mirrorBaseY - fixedY;
						var offsetedY = mirroredY + offset.Y;

						return offsetedY;
					}

					var newY = CalcY(sourceCanvasPos.Y);

					if (TGridCalculator.ConvertYToTGrid_DesignMode(newY, targetEditor) is not TGrid nt)
					{
						//todo warn
						return;
					}
					updateY(timelineObject, newY, nt);

					newTGrid = nt;
					//redo += () => timelineObject.TGrid = newTGrid.CopyNew();
					undo += () => timelineObject.TGrid = tGrid.CopyNew();

					switch (copied)
					{
						case Soflan or LaneBlockArea:
							ITimelineObject endIndicator = copied switch
							{
								Soflan _sfl => _sfl.EndIndicator,
								LaneBlockArea _lbk => _lbk.EndIndicator,
								_ => throw new Exception("这都能炸真的牛皮")
							};
							var oldEndIndicatorTGrid = endIndicator.TGrid.CopyNew();
							var endIndicatorY = TGridCalculator.ConvertTGridToY_DesignMode(oldEndIndicatorTGrid, sourceEditor);
							var newEndIndicatorY = CalcY(endIndicatorY);

							if (TGridCalculator.ConvertYToTGrid_DesignMode(newEndIndicatorY, targetEditor) is not TGrid newEndIndicatorTGrid)
							{
								//todo warn
								return;
							}

							updateY(endIndicator, newEndIndicatorY, newEndIndicatorTGrid);
							//redo += () => endIndicator.TGrid = newEndIndicatorTGrid.CopyNew();
							undo += () => endIndicator.TGrid = oldEndIndicatorTGrid.CopyNew();

							break;
						case ConnectableStartObject start:
							//apply child objects
							foreach (var child in start.Children)
							{
								var oldChildTGrid = child.TGrid.CopyNew();
								var y = TGridCalculator.ConvertTGridToY_DesignMode(oldChildTGrid, sourceEditor);
								var newChildY = CalcY(y);

								if (TGridCalculator.ConvertYToTGrid_DesignMode(newChildY, targetEditor) is not TGrid newChildTGrid)
								{
									//todo warn
									return;
								}

								updateY(child, newChildY, newChildTGrid);
								//redo += () => child.TGrid = newChildTGrid.CopyNew();
								undo += () => child.TGrid = oldChildTGrid.CopyNew();

								foreach (var control in child.PathControls)
								{
									var oldControlTGrid = control.TGrid.CopyNew();
									var cy = TGridCalculator.ConvertTGridToY_DesignMode(oldControlTGrid, sourceEditor);
									var newControlY = CalcY(cy);

									if (TGridCalculator.ConvertYToTGrid_DesignMode(newControlY, targetEditor) is not TGrid newControlTGrid)
									{
										//todo warn
										return;
									}

									updateY(control, newControlY, newControlTGrid);
									//redo += () => control.TGrid = newControlTGrid.CopyNew();
									undo += () => control.TGrid = oldControlTGrid.CopyNew();
								}
							}
							break;
						default:
							break;
					}
				}

				XGrid newXGrid = default;
				var offsetedX = 0d; //后面会用到,因此提出来
				if (copied is IHorizonPositionObject horizonPositionObject)
				{
					var xGrid = horizonPositionObject.XGrid.CopyNew();

					double CalcX(double x)
					{
						if (pasteOption == PasteOption.Direct)
							return x;

						var mirrorBaseX = mirrorXOpt is double _mirrorX ? _mirrorX : x;
						var mirroredX = mirrorBaseX + mirrorBaseX - x;

						return mirroredX + offset.X;
					}

					var mirrorBaseX = mirrorXOpt is double _mirrorX ? _mirrorX : sourceCanvasPos.X;
					var mirroredX = mirrorBaseX + mirrorBaseX - sourceCanvasPos.X;
					offsetedX = mirroredX + offset.X;
					var newX = CalcX(sourceCanvasPos.X);

					if (XGridCalculator.ConvertXToXGrid(newX, targetEditor) is not XGrid nx)
					{
						//todo warn
						return;
					}
					updateX(horizonPositionObject, newX, nx);

					newXGrid = nx;
					//redo += () => horizonPositionObject.XGrid = newXGrid.CopyNew();
					undo += () => horizonPositionObject.XGrid = xGrid.CopyNew();

					//apply child objects
					if (copied is ConnectableStartObject start)
					{
						foreach (var child in start.Children)
						{
							var oldChildXGrid = child.XGrid.CopyNew();
							var x = adjustX(XGridCalculator.ConvertXGridToX(oldChildXGrid, targetEditor));
							var newChildX = CalcX(x);

							if (XGridCalculator.ConvertXToXGrid(newChildX, targetEditor) is not XGrid newChildXGrid)
							{
								//todo warn
								return;
							}
							updateX(child, newChildX, newChildXGrid);

							//redo += () => child.XGrid = newChildXGrid.CopyNew();
							undo += () => child.XGrid = oldChildXGrid.CopyNew();

							foreach (var control in child.PathControls)
							{
								var oldControlXGrid = control.XGrid.CopyNew();
								var cx = XGridCalculator.ConvertXGridToX(oldControlXGrid, targetEditor);
								var newControlX = CalcX(cx);

								if (XGridCalculator.ConvertXToXGrid(newControlX, targetEditor) is not XGrid newControlXGrid)
								{
									//todo warn
									return;
								}
								updateX(control, newControlX, newControlXGrid);

								//redo += () => control.XGrid = newControlXGrid.CopyNew();
								undo += () => control.XGrid = oldControlXGrid.CopyNew();
							}
						}
					}
				}

				if (copied is IBulletPalleteReferencable bullet && bullet.ReferenceBulletPallete is BulletPallete pallete)
				{
					//如果IsAppend为false,那就直接改引用直接成这个。否则就新建一个
					var isAppend = false;
					BulletPallete existPallete = default;
					if (targetEditor.Fumen.BulletPalleteList.FirstOrDefault(x => x.StrID == pallete.StrID) is BulletPallete e)
					{
						existPallete = e;
						bool _check<T>(Func<BulletPallete, T> select)
							=> isAppend = isAppend || (Comparer<T>.Default.Compare(select(pallete), select(existPallete)) != 0);

						_check(x => x.TypeValue);
						_check(x => x.TargetValue);
						_check(x => x.ShooterValue);
						_check(x => x.EditorName);
						//_check(x => x.EditorAxuiliaryLineColor);
						_check(x => x.PlaceOffset);
						_check(x => x.SizeValue);
						//_check(x => x.Tag);
					}
					else
						isAppend = true;

					BulletPallete pickPallete = default;
					if (isAppend)
					{
						var newPallete = pallete.CopyNew() as BulletPallete;
						newPallete.EditorName = $"{(string.IsNullOrWhiteSpace(newPallete.EditorName) ? newPallete.StrID : newPallete.EditorName)} - {Resources.Copy}";
						pickPallete = newPallete;
					}
					else
					{
						pickPallete = existPallete;
					}

					redo += () =>
					{
						if (isAppend)
							targetEditor.Fumen.AddObject(pickPallete);
						bullet.ReferenceBulletPallete = pickPallete;
					};
					undo += () =>
					{
						if (isAppend)
							targetEditor.Fumen.RemoveObject(pickPallete);
						bullet.ReferenceBulletPallete = default;
					};
				}

				if (copied is ILaneDockable dockable)
				{
					var before = dockable.ReferenceLaneStart;

					redo += () =>
					{
						//这里做个检查吧:如果复制新的位置刚好也(靠近)在原来附着的轨道上，那就不变，否则就得清除ReferenceLaneStart
						//todo 后面可以做更细节的检查和变动
						if (dockable.ReferenceLaneStart is LaneStartBase beforeStart)
						{
							var needRedockLane = true;
							if (beforeStart.CalulateXGrid(newTGrid) is XGrid xGrid)
							{
								var x = XGridCalculator.ConvertXGridToX(xGrid, targetEditor);
								var diff = offsetedX - x;

								if (Math.Abs(diff) < 8)
								{
									//那就是在轨道上，不用动了！
									needRedockLane = false;
								}
								else
								{
									dockable.ReferenceLaneStart = default;
								}
							}

							if (needRedockLane)
							{
								var dockableLanes = targetEditor.Fumen.Lanes
									.GetVisibleStartObjects(newTGrid, newTGrid)
									.Where(x => x.IsDockableLane && x != beforeStart)
									.OrderBy(x => Math.Abs(x.LaneType - beforeStart.LaneType));

								var pickLane = dockableLanes.FirstOrDefault();

								//不在轨道上，那就清除惹
								//todo 重新钦定一个轨道
								dockable.ReferenceLaneStart = pickLane;
							}
						}
						else
							dockable.ReferenceLaneStart = default;
					};

					undo += () => dockable.ReferenceLaneStart = before;
				}

				var map = new Dictionary<ISelectableObject, bool>();
				foreach (var selectObj in ((copied as IDisplayableObject)?.GetDisplayableObjects() ?? Enumerable.Empty<IDisplayableObject>()).OfType<ISelectableObject>())
					map[selectObj] = selectObj.IsSelected;

				redo += () =>
				{
					targetEditor.Fumen.AddObject(copied);
					foreach ((var obj, var tuple) in posMap)
					{
						(var pos, var tGrid, var xGrid) = tuple;
						if (targetEditor.Setting.AdjustPastedObjects)
						{
							var interaction = targetEditor.InteractiveManager.GetInteractive(copied);
							interaction?.OnMoveCanvas(obj as OngekiObjectBase, pos, targetEditor);
						}
						else
						{
							if (obj is ITimelineObject timeline)
								timeline.TGrid = tGrid.CopyNew();
							if (obj is IHorizonPositionObject horizon)
								horizon.XGrid = xGrid.CopyNew();
						}
					}

					foreach (var selectObj in map.Keys)
						selectObj.IsSelected = true;
				};

				undo += () =>
				{
					targetEditor.RemoveObject(copied);
					foreach (var pair in map)
						pair.Key.IsSelected = pair.Value;
				};
			};

			redo += () => IoC.Get<IFumenObjectPropertyBrowser>().RefreshSelected(targetEditor);
			undo += () => IoC.Get<IFumenObjectPropertyBrowser>().RefreshSelected(targetEditor);

			targetEditor.UndoRedoManager.ExecuteAction(LambdaUndoAction.Create(Resources.CopyAndPaste, redo, undo));
		}
	}
	*/
	}
}