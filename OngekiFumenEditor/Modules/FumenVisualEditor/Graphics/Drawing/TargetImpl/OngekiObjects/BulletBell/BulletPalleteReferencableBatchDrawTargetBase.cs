using Caliburn.Micro;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.Collections;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Kernel.Graphics;
using OngekiFumenEditor.Kernel.Graphics.Base;
using OngekiFumenEditor.Utils;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Graphics.Drawing.TargetImpl.OngekiObjects.BulletBell
{
	public abstract class BulletPalleteReferencableBatchDrawTargetBase<T> : CommonBatchDrawTargetBase<T>, IDisposable where T : OngekiMovableObjectBase, IBulletPalleteReferencable
	{
		protected Dictionary<Texture, ConcurrentBag<(Vector2, Vector2, float)>> normalDrawList = new();
		protected Dictionary<Texture, ConcurrentBag<(Vector2, Vector2, float)>> selectedDrawList = new();
		protected List<(Vector2 pos, IBulletPalleteReferencable obj)> drawStrList = new();

		private readonly SoflanList nonSoflanList = new(new ISoflan[] { new Soflan() { TGrid = TGrid.Zero, Speed = 1 } });
		private readonly IStringDrawing stringDrawing;
		private readonly IHighlightBatchTextureDrawing highlightDrawing;
		private readonly IBatchTextureDrawing batchTextureDrawing;
		private readonly ParallelOptions parallelOptions;
		private readonly int parallelCountLimit;

		public BulletPalleteReferencableBatchDrawTargetBase()
		{
			stringDrawing = IoC.Get<IStringDrawing>();
			batchTextureDrawing = IoC.Get<IBatchTextureDrawing>();
			highlightDrawing = IoC.Get<IHighlightBatchTextureDrawing>();

			parallelOptions = new ParallelOptions()
			{
				MaxDegreeOfParallelism = Math.Max(2, Environment.ProcessorCount - 2),
			};

			Log.LogDebug($"BulletDrawingTarget.MaxDegreeOfParallelism = {parallelOptions.MaxDegreeOfParallelism}");

			parallelCountLimit = Properties.EditorGlobalSetting.Default.ParallelCountLimit;
		}

		public virtual void Dispose()
		{
			ClearDrawList();
		}

		public abstract void DrawVisibleObject_DesignMode(IFumenEditorDrawingContext target, T obj, Vector2 pos, float rotate);
		public abstract void DrawVisibleObject_PreviewMode(IFumenEditorDrawingContext target, T obj, Vector2 pos, float rotate);

		private void DrawEditorMode(IFumenEditorDrawingContext target, T obj)
		{
			var toX = XGridCalculator.ConvertXGridToX(obj.XGrid, target.Editor);
			var toTime = target.ConvertToY(obj.TGrid);

			var pos = new Vector2((float)toX, (float)toTime);
			DrawVisibleObject_DesignMode(target, obj, pos, 0);
		}

		private void DrawPallateStr(IDrawingContext target)
		{
			foreach ((var pos, var obj) in drawStrList)
			{
				if (obj.ReferenceBulletPallete is null)
					continue;
				stringDrawing.Draw($"{obj.ReferenceBulletPallete.StrID}", new(pos.X, pos.Y + 5), Vector2.One, 16, 0, Vector4.One, new(0.5f, 0.5f), default, target, default, out _);
			}
		}

		private void ClearDrawList()
		{
			foreach (var l in normalDrawList.Values)
				l.Clear();
			foreach (var l in selectedDrawList.Values)
				l.Clear();
			drawStrList.Clear();
		}

		private void DrawPreviewMode(IEnumerable<T> objs)
		{
			var currentTGrid = TGridCalculator.ConvertAudioTimeToTGrid(target.CurrentPlayTime, target.Editor);
			var judgeOffset = target.Editor.Setting.JudgeLineOffsetY;
			var baseY = Math.Min(target.Rect.MinY, target.Rect.MaxY) + judgeOffset;
			var scale = target.Editor.Setting.VerticalDisplayScale;
			var bpmList = target.Editor.Fumen.BpmList;
			var nonSoflanCurrentTime = convertToYNonSoflan(currentTGrid);
			var soflanCurrentTime = convertToY(currentTGrid);
			var height = target.Rect.Height;

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			double convertToYNonSoflan(TGrid tgrid)
			{
				return TGridCalculator.ConvertTGridToY_DesignMode(
					tgrid,
					nonSoflanList,
					bpmList,
					scale);
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			double convertToX(double xgrid)
			{
				return XGridCalculator.ConvertXGridToX(xgrid, target.Editor);
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			double convertToY(TGrid tgrid)
			{
				return target.ConvertToY(tgrid);
			}

			void _Draw(T obj)
			{
				var bulletPallateRefObj = obj as IBulletPalleteReferencable;
				/*
                --------------------------- toTime 
                        \
                         \
                          \
                           \
                            \
                             O      <- currentTime
                              bell
                               \
                                \
                                 \
                                  \
                                   \
                ---------------------------- fromTime = toTime - appearOffsetTime
                 */

				//计算向量化的物件运动时间
				var appearOffsetTime = height / (obj.ReferenceBulletPallete?.Speed ?? 1f);

				var toTime = 0d;
				var currentTime = 0d;

				var isEnableSoflan = bulletPallateRefObj.ReferenceBulletPallete?.IsEnableSoflan ?? true;

				if (isEnableSoflan)
				{
					toTime = convertToY(obj.TGrid);
					currentTime = soflanCurrentTime;
				}
				else
				{
					toTime = convertToYNonSoflan(obj.TGrid);
					currentTime = nonSoflanCurrentTime;
				}

				var fromTime = toTime - appearOffsetTime;
				var precent = (currentTime - fromTime) / appearOffsetTime;
				var timeY = baseY + height * (1 - precent);

				if (timeY > target.Rect.MaxY)
					return;
				//todo CheckVisible()这里是考虑到光焰那个Bell会残留，因为画轴速度太快（感觉是个bug但后面有精力再坐牢吧）
				if (timeY < target.Rect.MinY || (precent > 1 && !target.CheckVisible(obj.TGrid)))
					return;

				var fromX = convertToX(bulletPallateRefObj.ReferenceBulletPallete?.CalculateFromXGridTotalUnit(bulletPallateRefObj, target.Editor.Fumen) ?? obj.XGrid.TotalUnit);
				var toXUnit = bulletPallateRefObj.ReferenceBulletPallete?.CalculateToXGridTotalUnit(bulletPallateRefObj, target.Editor.Fumen) ?? obj.XGrid.TotalUnit;
                var toX = convertToX(toXUnit);
				var timeX = MathUtils.CalculateXFromTwoPointFormFormula(currentTime, fromX, fromTime, toX, toTime);

				if (!(target.Rect.MinX <= timeX && timeX <= target.Rect.MaxX))
					return;

				var rotate = (float)Math.Atan((toX - fromX) / (toTime - fromTime));
				var pos = new Vector2((float)timeX, (float)timeY);

				DrawVisibleObject_PreviewMode(target, obj, pos, rotate);
			}

			/*
             存在spd < 1或者soflan影响的子弹/bell物件。因此无法简单的使用二分法快速枚举筛选物件
             使用并行计算，将所有bell/bullet全部判断，当然判断的结果也能直接拿来做计算
             //todo 还能优化
             */
			if (objs.Count() < parallelCountLimit)
			{
				foreach (var obj in objs)
					_Draw(obj);
			}
			else
			{
				Parallel.ForEach(objs, parallelOptions, _Draw);
			}
		}

		public override void DrawBatch(IFumenEditorDrawingContext target, IEnumerable<T> objs)
		{
			if (target.Editor.IsDesignMode)
			{
				foreach (var obj in objs)
					DrawEditorMode(target, obj);
			}
			else
			{
				DrawPreviewMode(objs);
			}

			foreach (var item in selectedDrawList)
				highlightDrawing.Draw(target, item.Key, item.Value.OrderBy(x => x.Item2.Y));
			foreach (var item in normalDrawList)
				batchTextureDrawing.Draw(target, item.Key, item.Value.OrderBy(x => x.Item2.Y));

			if (target.Editor.IsDesignMode)
				DrawPallateStr(target);

			ClearDrawList();
		}
	}
}
