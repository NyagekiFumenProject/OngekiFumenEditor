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

        // 说明：除以下固定端点样式外，波形/物件/节拍/游标颜色与线宽均由用户设置驱动，
        // 在每帧 Draw 时从 DefaultWaveformSettings / AudioPlayerToolViewerSetting 读取。
        // 透明端点是渐变线段的固定端点颜色，不对外暴露为设置。
        private static readonly System.Numerics.Vector4 TransparentColor = new(1, 1, 1, 0);
        // Bullet/Bell 圆点样式（原内联字面量具名化，不作为设置）。
        private static readonly System.Numerics.Vector4 BulletCircleColor = new(1, 0, 1, 1);
        private static readonly System.Numerics.Vector4 BellCircleColor = new(1, 1, 0, 1);
        private const float ObjectCircleRadius = 5f;
        private const float ObjectCircleVerticalOffset = 10f;
        // 当前播放时间游标线宽，不随波形线宽设置变化。
        private const int CursorLineWidth = 2;
        private const int MaxWaveformLineWidth = 24;

        // 节奏曲线样式：属于可视化的固定几何比例，不跟随波形线宽设置。
        private const float RhythmCurveHeightWeight = 0.55f;   // 曲线高度（相对半高）
        private const int RhythmCurveLineWidth = 2;

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

        public override void Draw(IWaveformDrawingContext target, PeakPointCollection peakData, IDrawCommandListBuilder builder, bool drawWaveform = true)
        {
            ArgumentNullException.ThrowIfNull(builder, nameof(builder));

            // 颜色/线宽逐帧从用户设置读取（默认值与改造前字面量一致），设置页改动经 VM 订阅触发图块作废。
            var waveformSettings = Properties.DefaultWaveformSettings.Default;
            var viewerSettings = Properties.AudioPlayerToolViewerSetting.Default;

            var waveformColor = waveformSettings.WaveformFillColor.ToVector4();
            var cursorColor = waveformSettings.WaveformCursorColor.ToVector4();
            var beatLineColor = waveformSettings.WaveformBeatLineColor.ToVector4();
            var objectPlaceLineColor = waveformSettings.WaveformObjectPlaceLineColor.ToVector4();
            var holdLineColor = waveformSettings.WaveformHoldLineColor.ToVector4();
            var bodyLineWidth = Math.Clamp(viewerSettings.WaveformBodyLineWidth, 1, MaxWaveformLineWidth);
            var holdLineWidth = Math.Clamp(viewerSettings.WaveformHoldLineWidth, 1, MaxWaveformLineWidth);
            var markerLineWidth = Math.Clamp(viewerSettings.WaveformMarkerLineWidth, 1, MaxWaveformLineWidth);

            var width = target.CurrentDrawingTargetContext.ViewRelativeRect.Width;
            var height = target.CurrentDrawingTargetContext.ViewRelativeRect.Height;

            var curTime = target.CurrentTime;
            var fromTime = curTime - TimeSpan.FromMilliseconds(target.CurrentTimeXOffset * target.DurationMsPerPixel);
            var toTime = fromTime + TimeSpan.FromMilliseconds(width * target.DurationMsPerPixel);
            var curTimeGrid = target.EditorViewModel.ConvertAudioTimeToTGrid(curTime);
            (_, _, var currentMeter, var currentBpm) = TGridCalculator.GetCurrentTimeSignature(curTimeGrid, target.EditorViewModel.Fumen.BpmList, target.EditorViewModel.Fumen.MeterChanges);
            var durationMs = (toTime - fromTime).TotalMilliseconds;

            //绘制波形
            if (drawWaveform && option.ShowWaveform && peakData.Count != 0)
            {
                builder.PushModelMatrix(Matrix4x4.CreateScale(1, target.WaveformVecticalScale, 1f));
                cachedLineDrawList.Clear();
                try
                {
                    WaveformGeometry.Build(cachedLineDrawList, peakData, fromTime, toTime, width, height, waveformColor, WaveformGeometry.DefaultEdgeMarkerColor);
                    if (cachedLineDrawList.Count > 0)
                        builder.DrawSimpleLines(cachedLineDrawList, bodyLineWidth);
                }
                finally
                {
                    cachedLineDrawList.Clear();
                    builder.PopModelMatrix();
                }
            }

            //绘制节奏曲线：数据来自整首歌的一次性频谱分析，与离屏图块无关，所以这里逐帧实时绘制
            //（顶点数已按像素列降采样，只与视图宽度有关，与歌曲长度、缩放级别无关）。
            if (option.ShowRhythmCurve && target.RhythmCurve is { } rhythmCurve && rhythmCurve.FrameCount > 0)
            {
                var tone = new RhythmCurveTone(
                    waveformSettings.RhythmCurveGamma,
                    waveformSettings.RhythmCurveEmphasis);

                builder.PushModelMatrix(Matrix4x4.CreateScale(1, target.WaveformVecticalScale, 1f));
                cachedLineDrawList.Clear();
                try
                {
                    RhythmGeometry.BuildCurve(cachedLineDrawList, rhythmCurve, fromTime, toTime, width, height,
                        RhythmCurveHeightWeight, waveformSettings.WaveformRhythmCurveColor.ToVector4(), tone);
                    if (cachedLineDrawList.Count > 0)
                        builder.DrawSimpleLines(cachedLineDrawList, RhythmCurveLineWidth);
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
                            cachedLineDrawList.Add(new(new(fromX, y), holdLineColor, VertexDash.Solider));
                            cachedLineDrawList.Add(new(new(toX, y), holdLineColor, VertexDash.Solider));
                            cachedLineDrawList.Add(new(new(toX, y), TransparentColor, VertexDash.Solider));
                        }
                    }
                    builder.DrawSimpleLines(cachedLineDrawList, holdLineWidth);

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
                                cachedLineDrawList.Add(new(new(x, buttomY), objectPlaceLineColor, VertexDash.Solider));
                                cachedLineDrawList.Add(new(new(x, topY), objectPlaceLineColor, VertexDash.Solider));
                                cachedLineDrawList.Add(new(new(x, topY), TransparentColor, VertexDash.Solider));
                            }

                            if (type.HasFlag(ObjType.Bullet))
                                cachedCircleDrawList.Add(new CircleInstance(new(x, buttomY - ObjectCircleVerticalOffset), BulletCircleColor, true, ObjectCircleRadius, 0));

                            if (type.HasFlag(ObjType.Bell))
                                cachedCircleDrawList.Add(new CircleInstance(new(x, topY + ObjectCircleVerticalOffset), BellCircleColor, true, ObjectCircleRadius, 0));

                            if (type.HasFlag(ObjType.Flick))
                            {
                                //todo
                            }
                        }
                    }
                    builder.DrawSimpleLines(cachedLineDrawList, markerLineWidth);
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
                            cachedLineDrawList.Add(new(new(x, buttomY), beatLineColor, VertexDash.Solider));
                            cachedLineDrawList.Add(new(new(x, topY), beatLineColor, VertexDash.Solider));
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
                    builder.DrawSimpleLines(cachedLineDrawList, markerLineWidth);
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
                        cursorColor,
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
                    cachedLineDrawList.Add(new(new(indirectorX - 1.5f, -height / 2), cursorColor, VertexDash.Solider));
                    cachedLineDrawList.Add(new(new(indirectorX - 1.5f, +height / 2), cursorColor, VertexDash.Solider));
                    cachedLineDrawList.Add(new(new(indirectorX + 1.5f, +height / 2), cursorColor, VertexDash.Solider));
                    cachedLineDrawList.Add(new(new(indirectorX + 1.5f, -height / 2), cursorColor, VertexDash.Solider));
                    cachedLineDrawList.Add(new(new(indirectorX - 1.5f, -height / 2), cursorColor, VertexDash.Solider));
                }
                builder.DrawSimpleLines(cachedLineDrawList, CursorLineWidth);
                cachedLineDrawList.Clear();

                builder.DrawString(
                    $"{currentMeter.BunShi}/{currentMeter.Bunbo} BPM:{currentBpm.BPM}",
                    new System.Numerics.Vector2(indirectorX + 4, height / 2),
                    System.Numerics.Vector2.One,
                    15,
                    0,
                    cursorColor,
                    new System.Numerics.Vector2(0, 0),
                    FontStyle.Normal,
                    default);
            }
        }
    }
}
