using Caliburn.Micro;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.Collections;
using OngekiFumenEditor.Kernel.Audio;
using OngekiFumenEditor.Kernel.Graphics;
using OngekiFumenEditor.Modules.AudioPlayerToolViewer;
using OngekiFumenEditor.Utils;
using OngekiFumenEditor.Utils.ObjectPool;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Graphics.Drawing.Editors
{
    internal class DrawWaveformHelper
    {
        private IPolygonDrawing polygonDrawing;

        public DrawWaveformHelper()
        {
            polygonDrawing = IoC.Get<IPolygonDrawing>();
        }

        public void Draw(IFumenEditorDrawingContext target)
        {
            var audioViewer = IoC.Get<IAudioPlayerToolViewer>();

            if (audioViewer.AudioPlayer is not IAudioPlayer audio)
                return;
            if (audioViewer.Editor != target.Editor)
                return;
            if (target.Editor?.Fumen is not OngekiFumen fumen)
                return;

            var minVisibleCanvasY = Math.Max(0, target.Rect.MinY);
            var maxVisibleCanvasY = Math.Max(0, target.Rect.MaxY);
            var scale = target.Editor.Setting.VerticalDisplayScale;
            var offsetY = target.Editor.Setting.JudgeLineOffsetY;
            var tUnitLength = target.Editor.Setting.TGridUnitLength;
            var bpmList = fumen.BpmList;
            var meterList = fumen.MeterChanges;

            using var _d2 = ObjectPool<List<(TimeSpan audioTime, int meter, double y)>>.GetWithUsingDisposable(out var drawAreaList, out _);
            drawAreaList.Clear();

            using var _d1 = TGridCalculator.GetVisbleTimelines(
                bpmList,
                meterList,
                minVisibleCanvasY,
                maxVisibleCanvasY,
                offsetY,
                1,
                scale,
                tUnitLength
            ).ToListWithObjectPool(out var timelines);

            var endTGrid = TGridCalculator.ConvertYToTGrid(maxVisibleCanvasY, bpmList, scale, tUnitLength);
            var startTGrid = TGridCalculator.ConvertYToTGrid(minVisibleCanvasY, bpmList, scale, tUnitLength);

            void AppendTimeSignature(TGrid tGrid, double y)
            {
                var meter = TGridCalculator.GetCurrentTimeSignature(tGrid, bpmList, meterList, tUnitLength).meter;
                var audioTime = TGridCalculator.ConvertTGridToAudioTime(tGrid, bpmList, tUnitLength);
                drawAreaList.Add((audioTime, meter.BunShi, y));
            }

            AppendTimeSignature(startTGrid, minVisibleCanvasY);
            foreach (var tl in timelines)
                AppendTimeSignature(tl.tGrid, tl.y);
            AppendTimeSignature(endTGrid, maxVisibleCanvasY);

            //remove same each meter
            for (int i = 1; i < drawAreaList.Count; i++)
            {
                var cur = drawAreaList[i];
                var prev = drawAreaList[i - 1];

                if (cur.meter == prev.meter && drawAreaList.Count > 2)
                    drawAreaList.RemoveAt(i--);
            }

            //draw them
            for (int i = 1; i < drawAreaList.Count; i++)
            {
                var cur = drawAreaList[i];
                var prev = drawAreaList[i - 1];

                DrawInternal(target,audio, prev, cur);
            }
        }

        private void DrawInternal(IDrawingContext target, IAudioPlayer audio, (TimeSpan audioTime, int meter, double y) prev, (TimeSpan audioTime, int meter, double y) cur)
        {

        }
    }
}
