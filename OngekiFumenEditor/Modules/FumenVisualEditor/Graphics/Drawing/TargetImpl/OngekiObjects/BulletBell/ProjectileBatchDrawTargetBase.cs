using Caliburn.Micro;
using CommunityToolkit.HighPerformance.Helpers;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.Collections;
using OngekiFumenEditor.Base.EditorObjects;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Base.OngekiObjects.Lane;
using OngekiFumenEditor.Base.OngekiObjects.Projectiles;
using OngekiFumenEditor.Base.OngekiObjects.Projectiles.Enums;
using OngekiFumenEditor.Kernel.Graphics;
using OngekiFumenEditor.UI.Controls.ObjectInspector;
using OngekiFumenEditor.Utils;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Graphics.Drawing.TargetImpl.OngekiObjects.BulletBell
{
    public abstract class ProjectileBatchDrawTargetBase<T> : CommonBatchDrawTargetBase<T>, IDisposable where T : OngekiMovableObjectBase, IProjectile
    {
        protected Dictionary<IImage, ConcurrentBag<(Vector2, Vector2, float, Vector4)>> normalDrawList = new();
        protected Dictionary<IImage, ConcurrentBag<(Vector2, Vector2, float, Vector4)>> selectedDrawList = new();
        protected List<(Vector2 pos, string str)> drawStrList = new();

        private static readonly SoflanList nonSoflanList = new([new Soflan() { TGrid = TGrid.Zero, Speed = 1 }]);
        private IStringDrawing stringDrawing;
        private IHighlightBatchTextureDrawing highlightDrawing;
        private IBatchTextureDrawing batchTextureDrawing;
        private ParallelOptions parallelOptions;
        private int parallelCountLimit;

        public override void Initialize(IRenderManagerImpl impl)
        {
            stringDrawing = impl.StringDrawing;
            batchTextureDrawing = impl.BatchTextureDrawing;
            highlightDrawing = impl.HighlightBatchTextureDrawing;

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
            foreach ((var pos, var str) in drawStrList)
                stringDrawing.Draw($"{str}", new(pos.X, pos.Y + 5), Vector2.One, 16, 0, Vector4.One, new(0.5f, 0.5f), default, target, default, out _);
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
            var updater = new ProjectileUpdater(target, DrawVisibleObject_PreviewMode);

            /*
             存在spd < 1或者soflan影响的子弹/bell物件。因此无法简单的使用二分法快速枚举筛选物件
             使用并行计算，将所有bell/bullet全部判断，当然判断的结果也能直接拿来做计算
             //todo 还能优化
             */
            if (objs.Count() < parallelCountLimit)
            {
                foreach (var obj in objs)
                    updater.Draw(obj);
            }
            else
            {
                if (objs is List<T> list)
                {
                    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_items")]
                    extern static ref T[] GetListItems(List<T> li);
                    var listArray = GetListItems(list);
                    var listMemory = new Memory<T>(listArray, 0, list.Count);

                    ParallelHelper.ForEach(listMemory, updater);
                }
                else
                {
                    Parallel.ForEach(objs, parallelOptions, x=> updater.Invoke(ref x));
                }
            }
        }

        public readonly struct ProjectileUpdater : IRefAction<T>
        {
            private readonly TGrid currentTGrid;
            private readonly double judgeOffset;
            private readonly float rectMinY;
            private readonly float rectMaxY;
            private readonly float rectMinX;
            private readonly float rectMaxX;
            private readonly double baseY;
            private readonly double scale;
            private readonly BpmList bpmList;
            private readonly double nonSoflanCurrentTime;
            private readonly float height;
            private readonly int randomSeed;

            private static readonly SoflanList nonSoflanList = new([new Soflan() { TGrid = TGrid.Zero, Speed = 1 }]);
            private readonly IFumenEditorDrawingContext target;
            private readonly Action<IFumenEditorDrawingContext, T, Vector2, float> drawVisibleObject_PreviewMode;

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

            public ProjectileUpdater(IFumenEditorDrawingContext target, Action<IFumenEditorDrawingContext, T, Vector2, float> drawVisibleObject_PreviewMode)
            {
                currentTGrid = TGridCalculator.ConvertAudioTimeToTGrid(target.CurrentPlayTime, target.Editor);
                judgeOffset = target.Editor.Setting.JudgeLineOffsetY;
                rectMinY = target.CurrentDrawingTargetContext.Rect.MinY;
                rectMaxY = target.CurrentDrawingTargetContext.Rect.MaxY;
                rectMinX = target.CurrentDrawingTargetContext.Rect.MinX;
                rectMaxX = target.CurrentDrawingTargetContext.Rect.MaxX;
                baseY = Math.Min(rectMinY, rectMaxY) + judgeOffset;
                scale = target.Editor.Setting.VerticalDisplayScale;
                bpmList = target.Editor.Fumen.BpmList;
                nonSoflanCurrentTime = convertToYNonSoflan(currentTGrid);
                height = target.CurrentDrawingTargetContext.Rect.Height;
                randomSeed = BulletPallete.RandomSeed;
                this.target = target;
                this.drawVisibleObject_PreviewMode = drawVisibleObject_PreviewMode;
            }

            public void Invoke(ref T x)
            {
                Draw(x);
            }

            public void Draw(T obj)
            {
                var objXGridTotalUnit = obj.XGrid.TotalUnit;
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
                var appearOffsetTime = height / obj.Speed;

                var toTime = 0d;
                var currentTime = 0d;

                if (obj.IsEnableSoflan)
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

                if (timeY > rectMaxY)
                    return;
                //todo CheckVisible()这里是考虑到光焰那个Bell会残留，因为画轴速度太快（感觉是个bug但后面有精力再坐牢吧）
                if (timeY < rectMinY || (precent > 1 && !target.CheckVisible(obj.TGrid)))
                    return;

                var fromXUnit = 0d;
                var toXUnit = 0d;

                var fumen = target.Editor.Fumen;

                #region ToXUnit

                switch (obj.TargetValue)
                {
                    case Target.Player:
                        var frameOffset = (40f - 7.5f) / (0.47f * MathF.Min(obj.Speed, 1));
                        var targetAudioTime = TGridCalculator.ConvertTGridToAudioTime(obj.TGrid, fumen.BpmList) - TGridCalculator.ConvertFrameToAudioTime(frameOffset);
                        if (targetAudioTime < TimeSpan.Zero)
                            targetAudioTime = TimeSpan.Zero;

                        toXUnit = target.Editor.PlayerLocationRecorder.GetLocationXUnit(targetAudioTime);
                        toXUnit += objXGridTotalUnit;
                        break;
                    case Target.FixField:
                    default:
                        toXUnit = objXGridTotalUnit;
                        break;
                }

                var rosr = obj.RandomOffsetRange;
                if (rosr > 0)
                {
                    var id = obj.Id;
                    //不想用Random类，直接异或计算吧
                    var seed = Math.Abs((randomSeed * id + 123) * id ^ id);
                    var actualRandomOffset = (-rosr) + (seed % (rosr - (-rosr) + 1));
                    toXUnit += actualRandomOffset;
                }

                #endregion

                #region FromXUnit

                switch (obj.ShooterValue)
                {
                    case Shooter.TargetHead:
                        fromXUnit = toXUnit;
                        break;
                    case Shooter.Enemy:
                        var tGrid = obj.TGrid;
                        var enemyLane = fumen.Lanes.GetVisibleStartObjects(tGrid, tGrid).OfType<EnemyLaneStart>().LastOrDefault();
                        var xGrid = enemyLane?.CalulateXGrid(tGrid);
                        fromXUnit = xGrid?.TotalUnit ?? objXGridTotalUnit;
                        break;
                    case Shooter.Center:
                    default:
                        fromXUnit = 0;
                        break;
                }

                fromXUnit += obj.PlaceOffset;

                #endregion

                var fromX = convertToX(fromXUnit);
                var toX = convertToX(toXUnit);
                var timeX = MathUtils.CalculateXFromTwoPointFormFormula(currentTime, fromX, fromTime, toX, toTime);

                if (!(rectMinX <= timeX && timeX <= rectMaxX))
                    return;

                var rotate = (float)Math.Atan((toX - fromX) / (toTime - fromTime));
                var pos = new Vector2((float)timeX, (float)timeY);

                drawVisibleObject_PreviewMode(target, obj, pos, rotate);
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
