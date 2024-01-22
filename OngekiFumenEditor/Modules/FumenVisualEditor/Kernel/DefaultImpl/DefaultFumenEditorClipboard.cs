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
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using static OngekiFumenEditor.Base.OngekiObjects.Flick;
using static OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels.FumenVisualEditorViewModel;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Kernel.DefaultImpl
{
	[Export(typeof(IFumenEditorClipboard))]
	[PartCreationPolicy(CreationPolicy.Shared)]
	internal class DefaultFumenEditorClipboard : IFumenEditorClipboard
	{
		private Dictionary<OngekiObjectBase, Point> currentCopiedSources = new();
		private FumenVisualEditorViewModel sourceEditor;
		private double prevScale;

		public bool ContainPastableObjects => sourceEditor is not null && currentCopiedSources.Any();

		public IReadOnlyCollection<OngekiObjectBase> CurrentCopiedObjects => currentCopiedSources.Keys;

		public async Task CopyObjects(FumenVisualEditorViewModel sourceEditor, IEnumerable<ISelectableObject> objects)
		{
			if (sourceEditor.IsLocked)
				return;
			if (sourceEditor.Fumen is null)
				return;
			if (!sourceEditor.IsDesignMode)
			{
				sourceEditor.ToastNotify(Resources.EditorMustBeDesignMode);
				return;
			}
			if (objects.IsEmpty())
			{
				sourceEditor.ToastNotify(Resources.ClearCopyList);
				return;
			}

			prevScale = sourceEditor.Setting.VerticalDisplayScale;

			//清空一下
			currentCopiedSources.Clear();
			this.sourceEditor = default;

			Point CalcPos(ISelectableObject obj)
			{
				var x = 0d;
				if (obj is IHorizonPositionObject horizon)
					x = XGridCalculator.ConvertXGridToX(horizon.XGrid, sourceEditor);

				var y = 0d;
				if (obj is ITimelineObject timeline)
					y = TGridCalculator.ConvertTGridToY_DesignMode(timeline.TGrid, sourceEditor);

				return new Point(x, y);
			}

			foreach (var obj in objects.Where(x => x switch
			{
				//不允许被复制
				ConnectableObjectBase and not (ConnectableStartObject) => false,
				LaneCurvePathControlObject => false,
				LaneBlockArea.LaneBlockAreaEndIndicator => false,
				Soflan.SoflanEndIndicator => false,
				//允许被复制
				_ => true,
			}))
			{
				//这里还是得再次详细过滤:
				// * Hold头可以直接被复制
				// * 轨道如果是整个轨道节点都被选中，那么它也可以被复制，否则就不准
				if (obj is ConnectableStartObject start && obj is not Hold)
				{
					//检查start轨道节点是否全被选中了
					if (!start.Children.OfType<ConnectableObjectBase>().Append(start).All(x => x.IsSelected))
						continue;
				}

				var canvasPos = CalcPos(obj);

				var source = obj as OngekiObjectBase;
				var copied = source?.CopyNew();
				if (copied is null)
					continue;

				switch (copied)
				{
					//特殊处理ConnectableStart:连Child和Control一起复制了,顺便删除RecordId(添加时需要重新分配而已)
					case ConnectableStartObject _start:
						_start.CopyEntireConnectableObject((ConnectableStartObject)source);
						_start.RecordId = -1;
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
						break;
					case IBulletPalleteReferencable bulletPalleteObject:
						if (bulletPalleteObject.ReferenceBulletPallete is BulletPallete bpl)
							bulletPalleteObject.ReferenceBulletPallete = bpl;
						break;
					default:
						break;
				}

				//注册,并记录当前位置
				currentCopiedSources[copied] = canvasPos;
			}

			if (currentCopiedSources.Count == 0)
				sourceEditor.ToastNotify(Resources.ClearCopyList);
			else
			{
				this.sourceEditor = sourceEditor;
				sourceEditor.ToastNotify($"{Resources.CopiedObjects.Format(currentCopiedSources.Count)} {(currentCopiedSources.Count == 1 ? ("," + Resources.AsBrushSourceObject) : string.Empty)}");
			}
			return;
		}

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
						offsetedX = mirroredX + offset.X;

						return offsetedX;
					}

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
							var x = XGridCalculator.ConvertXGridToX(oldChildXGrid, targetEditor);
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

		private double? CalculateYMirror(IEnumerable<OngekiObjectBase> objects, PasteOption mirrorOption)
		{
			if (mirrorOption != PasteOption.SelectedRangeCenterTGridMirror)
				return null;

			(var minY, var maxY) = objects
					.Where(x => x is not ConnectableObjectBase)
					.Select(x => currentCopiedSources.TryGetValue(x, out var p) ? (true, p.Y) : default)
					.Where(x => x.Item1)
					.Select(x => x.Y)
					.MaxMinBy(x => x);

			var diffY = maxY - minY;
			var mirrorY = minY + diffY / 2f;
			return mirrorY;
		}

		private double? CalculateXMirror(FumenVisualEditorViewModel targetEditor, IEnumerable<OngekiObjectBase> objects, PasteOption mirrorOption)
		{
			if (mirrorOption == PasteOption.XGridZeroMirror)
				return XGridCalculator.ConvertXGridToX(0, targetEditor);

			if (mirrorOption == PasteOption.SelectedRangeCenterXGridMirror)
			{
				(var minX, var maxX) = objects
					.Where(x => x is not ConnectableObjectBase)
					.Select(x => currentCopiedSources.TryGetValue(x, out var p) ? (true, p.X) : default)
					.Where(x => x.Item1)
					.Select(x => x.X)
					.MaxMinBy(x => x);

				var diffX = maxX - minX;
				var mirrorX = minX + diffX / 2f;
				return mirrorX;
			}

			return null;
		}

		private Point CalculateRangeCenter(IEnumerable<OngekiObjectBase> objects)
		{
			var mesureObjects = objects;

			//如果是纯轨道复制，那么给所有轨道都计算,如果不是，就过滤掉所有轨道
			if (!mesureObjects.All(x => x is ConnectableObjectBase))
				mesureObjects = mesureObjects.Where(x => x is not ConnectableObjectBase);
			else
				mesureObjects = mesureObjects.Where(x => x is ConnectableStartObject);

			(var minX, var maxX) = mesureObjects
					.Select(x => currentCopiedSources.TryGetValue(x, out var p) ? (true, p.X) : default)
					.Where(x => x.Item1)
					.Select(x => x.X)
					.MaxMinBy(x => x);

			var diffX = maxX - minX;
			var x = minX + diffX / 2f;

			(var minY, var maxY) = mesureObjects
					.Select(x => currentCopiedSources.TryGetValue(x, out var p) ? (true, p.Y) : default)
					.Where(x => x.Item1)
					.Select(x => x.Y)
					.MaxMinBy(x => x);

			var diffY = maxY - minY;
			var y = minY + diffY / 2f;

			return new(x, y);
		}
	}
}
