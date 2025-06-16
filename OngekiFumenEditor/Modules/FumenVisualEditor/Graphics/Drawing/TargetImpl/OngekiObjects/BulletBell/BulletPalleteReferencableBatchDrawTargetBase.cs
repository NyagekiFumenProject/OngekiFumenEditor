﻿using Caliburn.Micro;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.Collections;
using OngekiFumenEditor.Base.EditorObjects;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Base.OngekiObjects.BulletPalleteEnums;
using OngekiFumenEditor.Base.OngekiObjects.Lane;
using OngekiFumenEditor.Kernel.Graphics;
using OngekiFumenEditor.Utils;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Graphics.Drawing.TargetImpl.OngekiObjects.BulletBell
{
    public abstract class BulletPalleteReferencableBatchDrawTargetBase<T> : CommonBatchDrawTargetBase<T>, IDisposable where T : OngekiMovableObjectBase, IBulletPalleteReferencable
    {
        protected Dictionary<IImage, ConcurrentBag<(Vector2, Vector2, float)>> normalDrawList = new();
        protected Dictionary<IImage, ConcurrentBag<(Vector2, Vector2, float)>> selectedDrawList = new();
        protected List<(Vector2 pos, IBulletPalleteReferencable obj)> drawStrList = new();

        private readonly SoflanList nonSoflanList = new([new Soflan() { TGrid = TGrid.Zero, Speed = 1 }]);
        private readonly IStringDrawing stringDrawing;
        private readonly IHighlightBatchTextureDrawing highlightDrawing;
        private readonly IBatchTextureDrawing batchTextureDrawing;
        private readonly ParallelOptions parallelOptions;
        private readonly int parallelCountLimit;

        public BulletPalleteReferencableBatchDrawTargetBase()
        {
            stringDrawing = IoC.Get<IDrawingManager>().StringDrawing;
            batchTextureDrawing = IoC.Get<IDrawingManager>().BatchTextureDrawing;
            highlightDrawing = IoC.Get<IDrawingManager>().HighlightBatchTextureDrawing;

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

        private void DrawDesignMode(IFumenEditorDrawingContext target, T obj)
        {
            var toX = XGridCalculator.ConvertXGridToX(obj.XGrid, target.Editor);
            var toTime = target.ConvertToY_DefaultSoflanGroup(obj.TGrid);

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

        private void DrawPreviewMode(IFumenEditorDrawingContext target, IEnumerable<T> objs)
        {
            var currentTGrid = TGridCalculator.ConvertAudioTimeToTGrid(target.CurrentPlayTime, target.Editor);
            var judgeOffset = target.Editor.Setting.JudgeLineOffsetY;
            var baseY = Math.Min(target.CurrentDrawingTargetContext.Rect.MinY, target.CurrentDrawingTargetContext.Rect.MaxY) + judgeOffset;
            var scale = target.Editor.Setting.VerticalDisplayScale;
            var bpmList = target.Editor.Fumen.BpmList;
            var nonSoflanCurrentTime = convertToYNonSoflan(currentTGrid);
            //var soflanCurrentTime = convertToY(currentTGrid, target.Editor.Fumen.SoflansMap.DefaultSoflanList);
            var height = target.CurrentDrawingTargetContext.Rect.Height;

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
            double convertToY(TGrid tgrid, SoflanList soflans)
            {
                return target.ConvertToY(tgrid, soflans);
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
                    var soflanList = target.Editor._cacheSoflanGroupRecorder.GetCache(obj);
                    toTime = convertToY(obj.TGrid, soflanList);
                    currentTime = convertToY(currentTGrid, soflanList);
                }
                else
                {
                    toTime = convertToYNonSoflan(obj.TGrid);
                    currentTime = nonSoflanCurrentTime;
                }

                var fromTime = toTime - appearOffsetTime;
                var precent = (currentTime - fromTime) / appearOffsetTime;
                var timeY = baseY + height * (1 - precent);

                if (timeY > target.CurrentDrawingTargetContext.Rect.MaxY)
                    return;
                //todo CheckVisible()这里是考虑到光焰那个Bell会残留，因为画轴速度太快（感觉是个bug但后面有精力再坐牢吧）
                if (timeY < target.CurrentDrawingTargetContext.Rect.MinY || (precent > 1 && !target.CheckVisible(obj.TGrid)))
                    return;

                var fromXUnit = 0d;
                var toXUnit = 0d;

                if (bulletPallateRefObj.ReferenceBulletPallete is BulletPallete pallete)
                {
                    var fumen = target.Editor.Fumen;

                    #region ToXUnit

                    switch (pallete.TargetValue)
                    {
                        case OngekiFumenEditor.Base.OngekiObjects.BulletPalleteEnums.Target.Player:
                            var frameOffset = (40f - 7.5f) / (0.47f * MathF.Min(pallete.Speed, 1));
                            var targetAudioTime = TGridCalculator.ConvertTGridToAudioTime(obj.TGrid, fumen.BpmList) - TGridCalculator.ConvertFrameToAudioTime(frameOffset);
                            if (targetAudioTime < TimeSpan.Zero)
                                targetAudioTime = TimeSpan.Zero;

                            toXUnit = target.Editor.PlayerLocationRecorder.GetLocationXUnit(targetAudioTime);
                            toXUnit += obj.XGrid.TotalUnit;
                            break;
                        case OngekiFumenEditor.Base.OngekiObjects.BulletPalleteEnums.Target.FixField:
                        default:
                            toXUnit = obj.XGrid.TotalUnit;
                            break;
                    }

                    var rosr = pallete.RandomOffsetRange;
                    if (rosr > 0)
                    {
                        var id = obj.Id;
                        //不想用Random类，直接异或计算吧
                        var seed = Math.Abs((BulletPallete.RandomSeed * id + 123) * id ^ id);
                        var actualRandomOffset = (-rosr) + (seed % (rosr - (-rosr) + 1));
                        toXUnit += actualRandomOffset;
                    }

                    #endregion

                    #region FromXUnit

                    switch (pallete.ShooterValue)
                    {
                        case OngekiFumenEditor.Base.OngekiObjects.BulletPalleteEnums.Shooter.TargetHead:
                            fromXUnit = toXUnit;
                            break;
                        case OngekiFumenEditor.Base.OngekiObjects.BulletPalleteEnums.Shooter.Enemy:
                            var tGrid = obj.TGrid;
                            var enemyLane = fumen.Lanes.GetVisibleStartObjects(tGrid, tGrid).OfType<EnemyLaneStart>().LastOrDefault();
                            var xGrid = enemyLane?.CalulateXGrid(tGrid);
                            fromXUnit = xGrid?.TotalUnit ?? obj.XGrid.TotalUnit;
                            break;
                        case OngekiFumenEditor.Base.OngekiObjects.BulletPalleteEnums.Shooter.Center:
                        default:
                            fromXUnit = 0;
                            break;
                    }

                    fromXUnit += pallete.PlaceOffset;

                    #endregion
                }
                else
                {
                    toXUnit = obj.XGrid.TotalUnit;
                    fromXUnit = toXUnit;
                }

                var fromX = convertToX(fromXUnit);
                var toX = convertToX(toXUnit);
                var timeX = MathUtils.CalculateXFromTwoPointFormFormula(currentTime, fromX, fromTime, toX, toTime);

                if (!(target.CurrentDrawingTargetContext.Rect.MinX <= timeX && timeX <= target.CurrentDrawingTargetContext.Rect.MaxX))
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
                    DrawDesignMode(target, obj);
            }
            else
            {
                DrawPreviewMode(target, objs);
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
