using OngekiFumenEditor.Avalonia.Base;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects;
using OngekiFumenEditor.Avalonia.Kernel.Graphics;
using OngekiFumenEditor.Avalonia.Kernel.Graphics.DrawCommands;
using OngekiFumenEditor.Avalonia.Utils;
using OngekiFumenEditor.Avalonia.Utils.ObjectPool;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Avalonia;
using Avalonia.Input;
using static OngekiFumenEditor.Avalonia.Kernel.Graphics.ILineDrawing;

namespace OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Graphics.Drawing.Editors
{
    public class DrawTimeSignatureHelper
    {
        public struct CacheDrawTimeLineResult
        {
            public double Y { get; set; }
            public string Display { get; set; }
        }

        private List<CacheDrawTimeLineResult> drawLines = new();


        public void Initalize(IRenderManagerImpl renderImpl)
        {
        }

        public void DrawLines(IFumenEditorDrawingContext target, IDrawCommandListBuilder builder)
        {
            drawLines.Clear();

            var fumen = target.Editor.EditorContext.Fumen;

            if (target.Editor.Setting.BeatSplit == 0)
                return;

            // 渲染一律以当帧绘制上下文为准：RectInDesignMode 只在设计模式更新，预览下会为默认值(宽高 0)或陈旧值。
            var drawingContext = target.CurrentDrawingTargetContext;

            IEnumerable<(TGrid tGrid, double y, int beatIndex, MeterChange meter, BPMChange bpm)> timelines = Enumerable.Empty<(TGrid tGrid, double y, int beatIndex, MeterChange meter, BPMChange bpm)>();
            if (target.Editor.IsDesignMode)
            {
                //todo 暂时显示默认的变速组
                timelines = TGridCalculator.GetVisbleTimelines_DesignMode(
                    fumen.SoflansMap.DefaultSoflanList,
                    fumen.BpmList,
                    fumen.MeterChanges,
                    Math.Max(0, drawingContext.WorldRect.MinY),
                    drawingContext.WorldRect.MaxY,
                    target.Editor.Setting.JudgeLineOffsetY,
                    target.Editor.Setting.BeatSplit,
                    target.Editor.Setting.VerticalDisplayScale
                );
            }
            else
            {
                var currentY = TGridCalculator.ConvertAudioTimeToY_PreviewMode(target.CurrentPlayTime, target.Editor);
                //todo 暂时显示默认的变速组
                timelines = TGridCalculator.GetVisbleTimelines_PreviewMode(
                    fumen.SoflansMap.DefaultSoflanList,
                    fumen.BpmList,
                    fumen.MeterChanges,
                    currentY,
                    drawingContext.ViewHeight,
                    target.Editor.Setting.JudgeLineOffsetY,
                    target.Editor.Setting.BeatSplit,
                    target.Editor.Setting.VerticalDisplayScale
                );
            }

            var viewWidth = drawingContext.ViewRelativeRect.Width;
            var transDisp = viewWidth * 0.4f;
            var maxDispAlpha = 0.3f;
            var minDispAlpha = 0f;

            var isPreviewMode = target.Editor.IsPreviewMode;

            if (isPreviewMode)
                timelines = timelines.Where(x => x.beatIndex == 0);
            else
                minDispAlpha = maxDispAlpha;
            var eDisp = viewWidth - transDisp;

            using var list = ObjectPool.GetPooledList<LineVertex>();

            var displayAudioTime = target.Editor.Setting.DisplayTimeFormat == Models.EditorSetting.TimeFormat.AudioTime;


            foreach ((var t, var y, var beatIndex, _, _) in timelines)
            {
                var str = string.Empty;
                if (displayAudioTime)
                {
                    var audioTime = TGridCalculator.ConvertTGridToAudioTime(t, target.Editor);
                    str = $"{audioTime.Minutes,-2}:{audioTime.Seconds,-2}:{audioTime.Milliseconds,-3}";
                }
                else
                    str = t.ToString();

                //timelines carry world Y; both the lines and the cached label need view-relative Y
                var viewRelativeY = y - drawingContext.ViewRelativeOriginY;
                var fy = (float)viewRelativeY;

                drawLines.Add(new()
                {
                    Display = str,
                    Y = viewRelativeY
                });

                var maxAlpha = maxDispAlpha;
                var minAlpha = minDispAlpha;

                if (!isPreviewMode)
                {
                    if (beatIndex == 0)
                        maxAlpha = minAlpha = 1;
                }

                list.Add(new(new(0, fy), new(1, 1, 1, 0), VertexDash.Solider));
                list.Add(new(new(0, fy), new(1, 1, 1, maxAlpha), VertexDash.Solider));
                list.Add(new(new(transDisp, fy), new(1, 1, 1, minAlpha), VertexDash.Solider));
                list.Add(new(new(eDisp, fy), new(1, 1, 1, minAlpha), VertexDash.Solider));
                list.Add(new(new(viewWidth, fy), new(1, 1, 1, maxAlpha), VertexDash.Solider));
                list.Add(new(new(viewWidth, fy), new(1, 1, 1, 0), VertexDash.Solider));
            }

            builder.DrawSimpleLines(list, 1);
        }

        public void DrawTimeSigntureText(IFumenEditorDrawingContext target, IDrawCommandListBuilder builder)
        {
            var rightColor = Vector4.One;
            var leftColor = new Vector4(1, 1, 1, 0);

            //in designMode, if mouse is close right then show the timeline on the left of the editor
            if (target.Editor.CurrentCursorPosition is Point p && target.Editor.IsDesignMode)
            {
                var mouseXPercent = p.X / target.Editor.ViewWidth;

                if (mouseXPercent >= 0.7)
                {
                    var alpha = OpenTK.Mathematics.MathHelper.MapRange(mouseXPercent, 0.7, 0.9, 1, 0);
                    rightColor = new(1, 1, 1, (float)alpha);
                    leftColor = new(1, 1, 1, 1 - (float)alpha);
                }
            }

            if (rightColor.W > 0)
            {
                foreach (var pair in drawLines)
                    builder.DrawString(
                    pair.Display,
                    new(target.Editor.ViewWidth - 2,
                    (float)pair.Y + 10),
                    Vector2.One,
                    12,
                    0,
                    rightColor,
                    new(1, 0.5f),
                    IStringDrawing.StringStyle.Normal,
                    default
                );
            }

            if (leftColor.W > 0)
            {
                foreach (var pair in drawLines)
                    builder.DrawString(
                        pair.Display,
                        new(0 + 2,
                        (float)pair.Y + 10),
                        Vector2.One,
                        12,
                        0,
                        leftColor,
                        new(0, 0.5f),
                        IStringDrawing.StringStyle.Normal,
                        default
                    );
            }
        }
    }
}


