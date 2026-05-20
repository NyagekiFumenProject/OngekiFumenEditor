using Caliburn.Micro;
using OngekiFumenEditor.Core.Base;
using OngekiFumenEditor.Core.Base.Collections;
using OngekiFumenEditor.Core.Base.EditorObjects;
using OngekiFumenEditor.Core.Base.OngekiObjects;

using OngekiFumenEditor.Core.Base.OngekiObjects.Lane;
using OngekiFumenEditor.Core.Base.OngekiObjects.Projectiles;
using OngekiFumenEditor.Core.Base.OngekiObjects.Projectiles.Enums;
using OngekiFumenEditor.Kernel.Graphics;
using OngekiFumenEditor.UI.Controls.ObjectInspector;
using OngekiFumenEditor.Utils;
using System;
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
        public sealed class DrawBuffer
        {
            public Dictionary<IImage, List<(Vector2, Vector2, float, Vector4)>> Normal;
            public Dictionary<IImage, List<(Vector2, Vector2, float, Vector4)>> Selected;
            public List<(Vector2 pos, string str)> StrList;
        }

        protected Dictionary<IImage, List<(Vector2, Vector2, float, Vector4)>> normalDrawList = new();
        protected Dictionary<IImage, List<(Vector2, Vector2, float, Vector4)>> selectedDrawList = new();
        protected List<(Vector2 pos, string str)> drawStrList = new();

        private DrawBuffer mainBuffer;

        private static readonly Comparison<(Vector2, Vector2, float, Vector4)> _yCompare =
            static (a, b) => a.Item2.Y.CompareTo(b.Item2.Y);

        private readonly SoflanList nonSoflanList = new([new Soflan() { TGrid = TGrid.Zero, Speed = 1 }]);
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

            mainBuffer = new DrawBuffer
            {
                Normal = normalDrawList,
                Selected = selectedDrawList,
                StrList = drawStrList,
            };
        }

        public virtual void Dispose()
        {
            ClearDrawList();
        }

        public abstract void DrawVisibleObject_DesignMode(IFumenEditorDrawingContext target, T obj, Vector2 pos, float rotate, DrawBuffer buffer);
        public abstract void DrawVisibleObject_PreviewMode(IFumenEditorDrawingContext target, T obj, Vector2 pos, float rotate, DrawBuffer buffer);

        private void DrawDesignMode(IFumenEditorDrawingContext target, T obj)
        {
            var toX = XGridCalculator.ConvertXGridToX(obj.XGrid, target.Editor);
            var toTime = target.ConvertToY_DefaultSoflanGroup(obj.TGrid);

            var pos = new Vector2((float)toX, (float)toTime);
            DrawVisibleObject_DesignMode(target, obj, pos, 0, mainBuffer);
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

        private DrawBuffer CreateThreadLocalBuffer()
        {
            var normal = new Dictionary<IImage, List<(Vector2, Vector2, float, Vector4)>>(normalDrawList.Count);
            foreach (var key in normalDrawList.Keys)
                normal[key] = new List<(Vector2, Vector2, float, Vector4)>();
            var selected = new Dictionary<IImage, List<(Vector2, Vector2, float, Vector4)>>(selectedDrawList.Count);
            foreach (var key in selectedDrawList.Keys)
                selected[key] = new List<(Vector2, Vector2, float, Vector4)>();
            return new DrawBuffer { Normal = normal, Selected = selected, StrList = null };
        }

        private void MergeBuffer(DrawBuffer local)
        {
            lock (normalDrawList)
            {
                foreach (var (key, list) in local.Normal)
                {
                    if (list.Count > 0 && normalDrawList.TryGetValue(key, out var dst))
                        dst.AddRange(list);
                }
                foreach (var (key, list) in local.Selected)
                {
                    if (list.Count > 0 && selectedDrawList.TryGetValue(key, out var dst))
                        dst.AddRange(list);
                }
            }
        }

        private void DrawPreviewMode(IFumenEditorDrawingContext target, IEnumerable<T> objs)
        {
            var currentTGrid = target.Editor.ConvertAudioTimeToTGrid(target.CurrentPlayTime);
            var judgeOffset = target.Editor.Setting.JudgeLineOffsetY;
            var rect = target.CurrentDrawingTargetContext.Rect;
            var rectMinX = rect.MinX;
            var rectMaxX = rect.MaxX;
            var rectMinY = rect.MinY;
            var rectMaxY = rect.MaxY;
            var baseY = Math.Min(rectMinY, rectMaxY) + judgeOffset;
            var scale = target.Editor.Setting.VerticalDisplayScale;
            var bpmList = target.Editor.Fumen.BpmList;
            var nonSoflanCurrentTime = convertToYNonSoflan(currentTGrid);
            //var soflanCurrentTime = convertToY(currentTGrid, target.Editor.Fumen.SoflansMap.DefaultSoflanList);
            var height = rect.Height;

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

            var randomSeed = BulletPallete.RandomSeed;

            void _Draw(T obj, DrawBuffer buffer)
            {
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

                //子弹完全经过画面所需的运动时间
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
                        toXUnit += obj.XGrid.TotalUnit;
                        break;
                    case Target.FixField:
                    default:
                        toXUnit = obj.XGrid.TotalUnit;
                        break;
                }

                var rosr = obj.RandomOffsetRange;
                if (rosr > 0)
                {
                    var id = obj.Id;
                    //不使用Random类，避免随机数干扰
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
                        fromXUnit = xGrid?.TotalUnit ?? obj.XGrid.TotalUnit;
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

                DrawVisibleObject_PreviewMode(target, obj, pos, rotate, buffer);
            }

            /*
             由于spd < 1或者soflan影响下的子弹/bell的轨迹是无法简单地使用二分法或者枚举筛选出来
             使用并行计算，对所有bell/bullet全部判断，虽然判断的结果也只直接传给后续绘制
             //todo 进一步优化
             */
            var totalCount = (objs as ICollection<T>)?.Count ?? objs.Count();
            if (totalCount < parallelCountLimit)
            {
                foreach (var obj in objs)
                    _Draw(obj, mainBuffer);
            }
            else
            {
                Parallel.ForEach(
                    objs,
                    parallelOptions,
                    CreateThreadLocalBuffer,
                    (obj, _, local) =>
                    {
                        _Draw(obj, local);
                        return local;
                    },
                    MergeBuffer);
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

            foreach (var (texture, list) in selectedDrawList)
            {
                if (list.Count == 0)
                    continue;
                list.Sort(_yCompare);
                highlightDrawing.Begin(target, texture);
                var span = CollectionsMarshal.AsSpan(list);
                for (int i = 0; i < span.Length; i++)
                {
                    ref var item = ref span[i];
                    highlightDrawing.PostSprite(item.Item1, item.Item2, item.Item3, item.Item4);
                }
                highlightDrawing.End();
            }
            foreach (var (texture, list) in normalDrawList)
            {
                if (list.Count == 0)
                    continue;
                list.Sort(_yCompare);
                batchTextureDrawing.Begin(target, texture);
                var span = CollectionsMarshal.AsSpan(list);
                for (int i = 0; i < span.Length; i++)
                {
                    ref var item = ref span[i];
                    batchTextureDrawing.PostSprite(item.Item1, item.Item2, item.Item3, item.Item4);
                }
                batchTextureDrawing.End();
            }

            if (target.Editor.IsDesignMode)
                DrawPallateStr(target);

            ClearDrawList();
        }
    }
}
