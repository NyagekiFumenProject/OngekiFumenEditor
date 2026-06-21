using Caliburn.Micro;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.Collections;
using OngekiFumenEditor.Kernel.Audio;
using OngekiFumenEditor.Kernel.Graphics;
using OngekiFumenEditor.Kernel.Graphics.Text;
using OngekiFumenEditor.Kernel.Graphics.DrawCommands;
using OngekiFumenEditor.Kernel.Graphics.DrawCommands.DefaultDrawCommands;
using OngekiFumenEditor.Modules.FumenVisualEditor;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using OngekiFumenEditor.Utils;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Numerics;
using static OngekiFumenEditor.Kernel.Graphics.ILineDrawing;

namespace OngekiFumenEditor.Modules.AudioPlayerToolViewer.Graphics.WaveformDrawing.DefaultImpls
{
    [Export(typeof(IWaveformDrawing))]
    public partial class DefaultWaveformDrawing : CommonWaveformDrawingBase
    {
        [Flags]
        private enum ObjType
        {
            None = 0,
            Default = 1,
            Bullet = 2,
            Bell = 4,
            Flick = 8,
        }

        private SoflanList dummySoflanList;
        private static readonly VertexDash InvailedLineDash = new VertexDash(2, 2);

        private static readonly System.Numerics.Vector4 TransparentColor = new(1, 1, 1, 0);
        private static readonly System.Numerics.Vector4 WhiteColor = new(1, 1, 1, 1);
        private static readonly System.Numerics.Vector4 IndirectorColor = new(1, 1, 0, 1);
        private static readonly System.Numerics.Vector4 BeatColor = new(1, 0, 0, 1);
        private static readonly System.Numerics.Vector4 ObjectPlaceColor = new(1, 1, 0, 1);
        private static readonly System.Numerics.Vector4 HoldColor = new(1, 1f, 0f, 0.75f);
        private static readonly System.Numerics.Vector4 WaveformFillColor = new(100 / 255.0f, 149 / 255.0f, 237 / 255.0f, 1);

        private static readonly List<LineVertex> cachedLineDrawList = new();
        private static readonly List<(float, string)> cachedPostDrawList = new();
        private static readonly List<CircleInstance> cachedCircleDrawList = new();
        private static readonly Dictionary<TGrid, ObjType> cachedObjTimeMap = new();

        private DefaultWaveformOption option = new();
        public override IWaveformDrawingOption Options => option;

        public override void Initialize(IRenderManagerImpl impl)
        {
            dummySoflanList = new SoflanList();
        }

        public override void Draw(IWaveformDrawingContext target, PeakPointCollection peakData, IDrawCommandListBuilder builder)
        {
            ArgumentNullException.ThrowIfNull(builder, nameof(builder));

            var width = target.CurrentDrawingTargetContext.ViewRelativeRect.Width;
            var height = target.CurrentDrawingTargetContext.ViewRelativeRect.Height;

            var curTime = target.CurrentTime;
            var fromTime = curTime - TimeSpan.FromMilliseconds(target.CurrentTimeXOffset * target.DurationMsPerPixel);
            var toTime = fromTime + TimeSpan.FromMilliseconds(width * target.DurationMsPerPixel);
            var curTimeGrid = target.EditorViewModel.ConvertAudioTimeToTGrid(curTime);
            (_, _, var currentMeter, var currentBpm) = TGridCalculator.GetCurrentTimeSignature(curTimeGrid, target.EditorViewModel.Fumen.BpmList, target.EditorViewModel.Fumen.MeterChanges);
            var durationMs = (toTime - fromTime).TotalMilliseconds;

            //绘制波形
            if (option.ShowWaveform && peakData.Count != 0)
            {
                (var minIndex, var maxIndex) = peakData.BinaryFindRangeIndex(fromTime, toTime);
                builder.PushModelMatrix(Matrix4x4.CreateScale(1, target.WaveformVecticalScale, 1f));
                cachedLineDrawList.Clear();
                try
                {
                    var prevX = 0f;

                    cachedLineDrawList.Add(new(new(-width / 2, 0), WhiteColor, InvailedLineDash));
                    for (int i = minIndex; i < maxIndex; i += 1)
                    {
                        var peakPoint = peakData[i];

                        var x = (float)(width * ((peakPoint.Time - fromTime).TotalMilliseconds / durationMs) - width / 2);
                        var yTop = height / 2 * peakPoint.Amplitudes[0];
                        var yButtom = -height / 2 * peakPoint.Amplitudes[1];

                        //lineDrawing.PostPoint(new(x, 0), WaveformFillColor, VertexDash.Solider);
                        cachedLineDrawList.Add(new(new(x, yTop), WaveformFillColor, VertexDash.Solider));
                        cachedLineDrawList.Add(new(new(x, yButtom), WaveformFillColor, VertexDash.Solider));
                        prevX = x;
                    }
                    cachedLineDrawList.Add(new(new(prevX, 0), WaveformFillColor, InvailedLineDash));
                    cachedLineDrawList.Add(new(new(width / 2, 0), WhiteColor, InvailedLineDash));
                    builder.DrawSimpleLines(cachedLineDrawList, 1);
                }
                finally
                {
                    cachedLineDrawList.Clear();
                    builder.PopModelMatrix();
                }
            }

            //绘制节奏线
            if (target.EditorViewModel is FumenVisualEditorViewModel editor)
            {
                var beginTime = fromTime.TotalSeconds < 0 ? TimeSpan.Zero : fromTime;
                var endTime = toTime > target.AudioTotalDuration ? target.AudioTotalDuration : toTime;

                var beginTGrid = target.EditorViewModel.ConvertAudioTimeToTGrid(beginTime);
                var endTGrid = target.EditorViewModel.ConvertAudioTimeToTGrid(endTime);
                var curTGrid = target.EditorViewModel.ConvertAudioTimeToTGrid(curTime);

                var bpmList = editor.Fumen.BpmList;

                //var beginX = TGridCalculator.ConvertTGridToAudioTime(beginTGrid, bpmList).TotalMilliseconds;
                //var endX = TGridCalculator.ConvertTGridToAudioTime(endTGrid, bpmList).TotalMilliseconds;
                //var curX = TGridCalculator.ConvertTGridToAudioTime(curTGrid, bpmList).TotalMilliseconds;

                var beginX = beginTime.TotalMilliseconds;
                var endX = endTime.TotalMilliseconds;
                var curX = curTime.TotalMilliseconds;

                var aWidth = (endTime - beginTime).TotalMilliseconds / target.DurationMsPerPixel;
                var prefixOffsetX = -Math.Min(0, fromTime.TotalMilliseconds) / target.DurationMsPerPixel;
                var xWidth = endX - beginX;

                if (option.ShowObjectPlaceLine)
                {
                    cachedCircleDrawList.Clear();

                    void applyObjCounting(IEnumerable<ITimelineObject> timelineObjects, ObjType type)
                    {
                        foreach (var timeObj in timelineObjects)
                        {
                            var t = cachedObjTimeMap.TryGetValue(timeObj.TGrid, out var _t) ? _t : ObjType.None;
                            cachedObjTimeMap[timeObj.TGrid] = type | t;
                        }
                    }

                    var fumen = editor.Fumen;
                    applyObjCounting(fumen.Taps.BinaryFindRange(beginTGrid, endTGrid), ObjType.Default);
                    applyObjCounting(fumen.Bullets.BinaryFindRange(beginTGrid, endTGrid), ObjType.Bullet);
                    applyObjCounting(fumen.Bells.BinaryFindRange(beginTGrid, endTGrid), ObjType.Bell);
                    applyObjCounting(fumen.Beams.GetVisibleStartObjects(beginTGrid, endTGrid), ObjType.Default);
                    applyObjCounting(fumen.Flicks.BinaryFindRange(beginTGrid, endTGrid), ObjType.Flick);

                    float calcX(TGrid tGrid)
                    {
                        var bx = TGridCalculator.ConvertTGridToY_DesignMode(tGrid, dummySoflanList, bpmList, 1);
                        var x = (float)(prefixOffsetX + aWidth * ((bx - beginX) / xWidth) - width / 2);

                        return x;
                    }

                    var beatHeightWeight = 0.75f;
                    cachedLineDrawList.Clear();
                    foreach (var hold in fumen.Holds.GetVisibleStartObjects(beginTGrid, endTGrid))
                    {
                        var t = cachedObjTimeMap.TryGetValue(hold.TGrid, out var _t) ? _t : 0;
                        cachedObjTimeMap[hold.TGrid] = t | ObjType.Default;
                        if (hold?.HoldEnd?.TGrid is TGrid et)
                        {
                            var fromX = calcX(hold.TGrid);
                            var toX = calcX(et);
                            var y = 0;

                            cachedLineDrawList.Add(new(new(fromX, y), TransparentColor, VertexDash.Solider));
                            cachedLineDrawList.Add(new(new(fromX, y), HoldColor, VertexDash.Solider));
                            cachedLineDrawList.Add(new(new(toX, y), HoldColor, VertexDash.Solider));
                            cachedLineDrawList.Add(new(new(toX, y), TransparentColor, VertexDash.Solider));
                        }
                    }
                    builder.DrawSimpleLines(cachedLineDrawList, 4);

                    cachedLineDrawList.Clear();
                    {
                        var topY = height / 2 * beatHeightWeight;
                        var buttomY = -topY;

                        foreach (var pair in cachedObjTimeMap)
                        {
                            var tGrid = pair.Key;
                            var x = calcX(tGrid);

                            var type = pair.Value;

                            if (type.HasFlag(ObjType.Default))
                            {
                                cachedLineDrawList.Add(new(new(x, buttomY), TransparentColor, VertexDash.Solider));
                                cachedLineDrawList.Add(new(new(x, buttomY), ObjectPlaceColor, VertexDash.Solider));
                                cachedLineDrawList.Add(new(new(x, topY), ObjectPlaceColor, VertexDash.Solider));
                                cachedLineDrawList.Add(new(new(x, topY), TransparentColor, VertexDash.Solider));
                            }

                            if (type.HasFlag(ObjType.Bullet))
                                cachedCircleDrawList.Add(new CircleInstance(new(x, buttomY - 10), new(1, 0, 1, 1), true, 5f, 0));

                            if (type.HasFlag(ObjType.Bell))
                                cachedCircleDrawList.Add(new CircleInstance(new(x, topY + 10), new(1, 1, 0, 1), true, 5f, 0));

                            if (type.HasFlag(ObjType.Flick))
                            {
                                //todo
                            }
                        }
                    }
                    builder.DrawSimpleLines(cachedLineDrawList, 2);
                    builder.DrawCircles(cachedCircleDrawList);
                    cachedLineDrawList.Clear();
                    cachedCircleDrawList.Clear();
                }

                if (option.ShowTimingLine)
                {
                    cachedPostDrawList.Clear();
                    cachedLineDrawList.Clear();

                    {
                        var prevMeter = currentMeter;
                        var prevBpm = currentBpm;

                        foreach ((var tGrid, var bx, var beatIdx, var meter, var bpm) in TGridCalculator.GetVisbleTimelines_DesignMode(dummySoflanList, bpmList,
                            editor.Fumen.MeterChanges, beginX, endX, curX, editor.Setting.BeatSplit, 1.0f))
                        {
                            var x = (float)(prefixOffsetX + aWidth * ((bx - beginX) / xWidth) - width / 2);

                            var beatHeightWeight = beatIdx == 0 ? 0.75f : 0.5f;
                            beatHeightWeight = cachedObjTimeMap.ContainsKey(tGrid) ? 0.1f : beatHeightWeight;
                            var topY = height / 2 * beatHeightWeight;
                            var buttomY = -topY;


                            cachedLineDrawList.Add(new(new(x, buttomY), TransparentColor, VertexDash.Solider));
                            cachedLineDrawList.Add(new(new(x, buttomY), BeatColor, VertexDash.Solider));
                            cachedLineDrawList.Add(new(new(x, topY), BeatColor, VertexDash.Solider));
                            cachedLineDrawList.Add(new(new(x, topY), TransparentColor, VertexDash.Solider));

                            var str = "";
                            if (prevMeter != meter)
                                str += $"{meter.BunShi}/{meter.Bunbo}";
                            if (prevBpm != bpm)
                                str += $" BPM:{bpm.BPM}";
                            if (str.Length > 0)
                                cachedPostDrawList.Add((x + 2, str));

                            prevMeter = meter;
                            prevBpm = bpm;
                        }
                    }
                    builder.DrawSimpleLines(cachedLineDrawList, 2);
                    cachedLineDrawList.Clear();

                    //绘制提示
                    foreach ((var x, var str) in cachedPostDrawList)
                    {
                        builder.DrawString(
                        str,
                        new System.Numerics.Vector2(x, -height / 2),
                        System.Numerics.Vector2.One,
                        15,
                        0,
                        IndirectorColor,
                        new System.Numerics.Vector2(0, 2),
                        FontStyle.Normal,
                        default);
                    }
                }

                cachedObjTimeMap.Clear();
            }

            //绘制当前播放时间游标
            {
                var indirectorX = (float)(width * ((curTime - fromTime).TotalMilliseconds / durationMs) - width / 2);

                cachedLineDrawList.Clear();
                {
                    cachedLineDrawList.Add(new(new(indirectorX - 1.5f, -height / 2), IndirectorColor, VertexDash.Solider));
                    cachedLineDrawList.Add(new(new(indirectorX - 1.5f, +height / 2), IndirectorColor, VertexDash.Solider));
                    cachedLineDrawList.Add(new(new(indirectorX + 1.5f, +height / 2), IndirectorColor, VertexDash.Solider));
                    cachedLineDrawList.Add(new(new(indirectorX + 1.5f, -height / 2), IndirectorColor, VertexDash.Solider));
                    cachedLineDrawList.Add(new(new(indirectorX - 1.5f, -height / 2), IndirectorColor, VertexDash.Solider));
                }
                builder.DrawSimpleLines(cachedLineDrawList, 2);
                cachedLineDrawList.Clear();

                builder.DrawString(
                    $"{currentMeter.BunShi}/{currentMeter.Bunbo} BPM:{currentBpm.BPM}",
                    new System.Numerics.Vector2(indirectorX + 4, height / 2),
                    System.Numerics.Vector2.One,
                    15,
                    0,
                    IndirectorColor,
                    new System.Numerics.Vector2(0, 0),
                    FontStyle.Normal,
                    default);
            }
        }
    }
}
