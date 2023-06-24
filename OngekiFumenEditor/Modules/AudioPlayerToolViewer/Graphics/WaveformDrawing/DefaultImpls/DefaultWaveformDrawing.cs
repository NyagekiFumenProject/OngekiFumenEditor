using Caliburn.Micro;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.Collections;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Kernel.Audio;
using OngekiFumenEditor.Kernel.Graphics;
using OngekiFumenEditor.Modules.FumenVisualEditor;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using OngekiFumenEditor.Utils;
using OngekiFumenEditor.Utils.ObjectPool;
using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using static OngekiFumenEditor.Kernel.Audio.ISamplePeak;
using static OngekiFumenEditor.Kernel.Graphics.ILineDrawing;

namespace OngekiFumenEditor.Modules.AudioPlayerToolViewer.Graphics.WaveformDrawing.DefaultImpls
{
    [Export(typeof(IWaveformDrawing))]
    public partial class DefaultWaveformDrawing : CommonWaveformDrawingBase
    {
        private readonly ISimpleLineDrawing lineDrawing;
        private readonly IStringDrawing stringDrawing;
        private readonly SoflanList dummySoflanList;
        private static readonly VertexDash InvailedLineDash = new VertexDash() { DashSize = 2, GapSize = 2 };

        private static readonly System.Numerics.Vector4 TransparentColor = new(1, 1, 1, 0);
        private static readonly System.Numerics.Vector4 WhiteColor = new(1, 1, 1, 1);
        private static readonly System.Numerics.Vector4 IndirectorColor = new(1, 1, 0, 1);
        private static readonly System.Numerics.Vector4 BeatColor = new(1, 0, 0, 1);
        private static readonly System.Numerics.Vector4 ObjectPlaceColor = new(1, 1, 0, 1);
        private static readonly System.Numerics.Vector4 WaveformFillColor = new(100 / 255.0f, 149 / 255.0f, 237 / 255.0f, 1);

        private static readonly List<(float, string)> cachedPostDrawList = new();
        private static readonly Dictionary<TGrid, int> cachedObjTimeMap = new();

        private DefaultWaveformOption option = new();
        public override IWaveformDrawingOption Options => option;

        public DefaultWaveformDrawing()
        {
            lineDrawing = IoC.Get<ISimpleLineDrawing>();
            stringDrawing = IoC.Get<IStringDrawing>();
            dummySoflanList = new SoflanList();
        }

        public override void Draw(IWaveformDrawingContext target, PeakPointCollection peakData)
        {
            cachedPostDrawList.Clear();

            var width = target.ViewWidth;
            var height = target.ViewHeight;

            var curTime = target.CurrentTime;
            var fromTime = curTime - TimeSpan.FromMilliseconds(target.CurrentTimeXOffset * target.DurationMsPerPixel);
            var toTime = fromTime + TimeSpan.FromMilliseconds(width * target.DurationMsPerPixel);
            var curTimeGrid = TGridCalculator.ConvertAudioTimeToTGrid(curTime, target.EditorViewModel);
            (_, _, var currentMeter, var currentBpm) = TGridCalculator.GetCurrentTimeSignature(curTimeGrid, target.EditorViewModel.Fumen.BpmList, target.EditorViewModel.Fumen.MeterChanges, target.EditorViewModel.Setting.TGridUnitLength);
            var durationMs = (toTime - fromTime).TotalMilliseconds;

            (var minIndex, var maxIndex) = peakData.BinaryFindRangeIndex(fromTime, toTime);

            //绘制波形
            lineDrawing.PushOverrideModelMatrix(lineDrawing.GetOverrideModelMatrix() * Matrix4.CreateScale(1, target.WaveformVecticalScale, 1f));
            lineDrawing.Begin(target, 1);
            {
                var prevX = 0f;
                lineDrawing.PostPoint(new(-width / 2, 0), WhiteColor, InvailedLineDash);
                for (int i = minIndex; i < maxIndex; i++)
                {
                    var peakPoint = peakData[i];

                    var x = (float)(width * ((peakPoint.Time - fromTime).TotalMilliseconds / durationMs) - width / 2);
                    var yTop = height / 2 * peakPoint.Amplitudes[0];
                    var yButtom = -height / 2 * peakPoint.Amplitudes[1];

                    lineDrawing.PostPoint(new(x, 0), WaveformFillColor, VertexDash.Solider);
                    lineDrawing.PostPoint(new(x, yTop), WaveformFillColor, VertexDash.Solider);
                    lineDrawing.PostPoint(new(x, yButtom), WaveformFillColor, VertexDash.Solider);
                    prevX = x;
                }
                lineDrawing.PostPoint(new(prevX, 0), WaveformFillColor, InvailedLineDash);
                lineDrawing.PostPoint(new(width / 2, 0), WhiteColor, InvailedLineDash);
            }
            lineDrawing.End();
            lineDrawing.PopOverrideModelMatrix(out _);

            //绘制节奏线
            if (target.EditorViewModel is FumenVisualEditorViewModel editor)
            {
                var beginTime = fromTime.TotalSeconds < 0 ? TimeSpan.Zero : fromTime;
                var endTime = toTime > target.AudioTotalDuration ? target.AudioTotalDuration : toTime;

                var beginTGrid = TGridCalculator.ConvertAudioTimeToTGrid(beginTime, target.EditorViewModel);
                var endTGrid = TGridCalculator.ConvertAudioTimeToTGrid(endTime, target.EditorViewModel);
                var curTGrid = TGridCalculator.ConvertAudioTimeToTGrid(curTime, target.EditorViewModel);

                var bpmList = editor.Fumen.BpmList;
                var tGridUnitLength = editor.Setting.TGridUnitLength;

                var beginX = TGridCalculator.ConvertTGridToAudioTime(beginTGrid, bpmList, tGridUnitLength).TotalMilliseconds;
                var endX = TGridCalculator.ConvertTGridToAudioTime(endTGrid, bpmList, tGridUnitLength).TotalMilliseconds;
                var curX = TGridCalculator.ConvertTGridToAudioTime(curTGrid, bpmList, tGridUnitLength).TotalMilliseconds;

                var aWidth = (endTime - beginTime).TotalMilliseconds / target.DurationMsPerPixel;
                var prefixOffsetX = -Math.Min(0, fromTime.TotalMilliseconds) / target.DurationMsPerPixel;
                var xWidth = endX - beginX;

                if (option.ShowObjectPlaceLine)
                {
                    void applyObjCounting(IEnumerable<ITimelineObject> timelineObjects)
                    {
                        foreach (var timeObj in timelineObjects)
                        {
                            var t = cachedObjTimeMap.TryGetValue(timeObj.TGrid, out var _t) ? _t : 0;
                            cachedObjTimeMap[timeObj.TGrid] = t + 1;
                        }
                    }

                    var fumen = editor.Fumen;
                    applyObjCounting(fumen.Taps.BinaryFindRange(beginTGrid, endTGrid));
                    applyObjCounting(fumen.Bullets.BinaryFindRange(beginTGrid, endTGrid));
                    applyObjCounting(fumen.Bells.BinaryFindRange(beginTGrid, endTGrid));
                    applyObjCounting(fumen.Beams.GetVisibleStartObjects(beginTGrid, endTGrid));
                    applyObjCounting(fumen.Flicks.BinaryFindRange(beginTGrid, endTGrid));
                    foreach (var hold in fumen.Holds.GetVisibleStartObjects(beginTGrid, endTGrid))
                    {
                        var t = cachedObjTimeMap.TryGetValue(hold.TGrid, out var _t) ? _t : 0;
                        cachedObjTimeMap[hold.TGrid] = t + 1;
                        if (hold?.HoldEnd?.TGrid is TGrid et)
                        {
                            t = cachedObjTimeMap.TryGetValue(et, out _t) ? _t : 0;
                            cachedObjTimeMap[et] = t + 1;
                        }
                    }

                    lineDrawing.Begin(target, 2);
                    {
                        var beatHeightWeight = 0.85f;
                        var topY = height / 2 * beatHeightWeight;
                        var buttomY = -topY;

                        foreach (var pair in cachedObjTimeMap)
                        {
                            var tGrid = pair.Key;
                            var bx = TGridCalculator.ConvertTGridToY_DesignMode(tGrid, dummySoflanList, bpmList, 1, editor.Setting.TGridUnitLength);
                            var x = (float)(prefixOffsetX + aWidth * ((bx - beginX) / xWidth) - width / 2);

                            lineDrawing.PostPoint(new(x, buttomY), TransparentColor, VertexDash.Solider);
                            lineDrawing.PostPoint(new(x, buttomY), ObjectPlaceColor, VertexDash.Solider);
                            lineDrawing.PostPoint(new(x, topY), ObjectPlaceColor, VertexDash.Solider);
                            lineDrawing.PostPoint(new(x, topY), TransparentColor, VertexDash.Solider);
                        }
                    }
                    lineDrawing.End();
                }

                if (option.ShowTimingLine)
                {
                    lineDrawing.Begin(target, 2);
                    {
                        var prevMeter = currentMeter;
                        var prevBpm = currentBpm;

                        foreach ((var tGrid, var bx, var beatIdx, var meter, var bpm) in TGridCalculator.GetVisbleTimelines_DesignMode(dummySoflanList, bpmList,
                            editor.Fumen.MeterChanges, beginX, endX, curX, editor.Setting.BeatSplit, 1.0f, editor.Setting.TGridUnitLength))
                        {
                            var x = (float)(prefixOffsetX + aWidth * ((bx - beginX) / xWidth) - width / 2);

                            var beatHeightWeight = beatIdx == 0 ? 1 : 0.85f;
                            var topY = height / 2 * beatHeightWeight;
                            var buttomY = -topY;

                            var beatColor = cachedObjTimeMap.ContainsKey(tGrid) ? TransparentColor : BeatColor;

                            lineDrawing.PostPoint(new(x, buttomY), TransparentColor, VertexDash.Solider);
                            lineDrawing.PostPoint(new(x, buttomY), beatColor, VertexDash.Solider);
                            lineDrawing.PostPoint(new(x, topY), beatColor, VertexDash.Solider);
                            lineDrawing.PostPoint(new(x, topY), TransparentColor, VertexDash.Solider);

                            var str = "";
                            if (prevMeter != meter)
                                str += $"{meter.Bunbo}/{meter.BunShi}";
                            if (prevBpm != bpm)
                                str += $" BPM:{bpm.BPM}";
                            if (str.Length > 0)
                                cachedPostDrawList.Add((x + 2, str));

                            prevMeter = meter;
                            prevBpm = bpm;
                        }
                    }
                    lineDrawing.End();
                }

                //绘制提示
                foreach ((var x, var str) in cachedPostDrawList)
                {
                    stringDrawing.Draw(
                    str,
                    new System.Numerics.Vector2(x, -height / 2),
                    System.Numerics.Vector2.One,
                    15,
                    0,
                    IndirectorColor,
                    new System.Numerics.Vector2(0, 2),
                    IStringDrawing.StringStyle.Normal,
                    target,
                    default, out _);
                }

                cachedObjTimeMap.Clear();
            }

            //绘制当前播放时间游标
            {
                var indirectorX = (float)(width * ((curTime - fromTime).TotalMilliseconds / durationMs) - width / 2);

                lineDrawing.Begin(target, 2);
                {
                    lineDrawing.PostPoint(new(indirectorX - 1.5f, -height / 2), IndirectorColor, VertexDash.Solider);
                    lineDrawing.PostPoint(new(indirectorX - 1.5f, +height / 2), IndirectorColor, VertexDash.Solider);
                    lineDrawing.PostPoint(new(indirectorX + 1.5f, +height / 2), IndirectorColor, VertexDash.Solider);
                    lineDrawing.PostPoint(new(indirectorX + 1.5f, -height / 2), IndirectorColor, VertexDash.Solider);
                    lineDrawing.PostPoint(new(indirectorX - 1.5f, -height / 2), IndirectorColor, VertexDash.Solider);
                }
                lineDrawing.End();

                stringDrawing.Draw(
                    $"{currentMeter.Bunbo}/{currentMeter.BunShi} BPM:{currentBpm.BPM}",
                    new System.Numerics.Vector2(indirectorX + 4, height / 2),
                    System.Numerics.Vector2.One,
                    15,
                    0,
                    IndirectorColor,
                    new System.Numerics.Vector2(0, 0),
                    IStringDrawing.StringStyle.Normal,
                    target,
                    default, out _);
            }
        }
    }
}
