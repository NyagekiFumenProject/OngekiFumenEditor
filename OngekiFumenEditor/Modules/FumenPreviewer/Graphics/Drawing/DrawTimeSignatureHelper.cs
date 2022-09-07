using OngekiFumenEditor.Base;
using OngekiFumenEditor.Modules.FumenVisualEditor;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Modules.FumenPreviewer.Graphics.Drawing
{
    public class DrawTimeSignatureHelper : CommonLinesDrawTargetBase<OngekiObjectBase>
    {
        public struct CacheDrawLineResult
        {
            public double Y { get; set; }
            public TGrid TGrid { get; set; }
        }

        public override IEnumerable<string> DrawTargetID => throw new NotImplementedException();

        private List<CacheDrawLineResult> drawLines = new();

        public override void FillLine(List<LinePoint> list, OngekiObjectBase obj, OngekiFumen fumen)
        {
            drawLines.Clear();

            var timelines = TGridCalculator.GetVisbleTimelines(
                fumen.BpmList,
                fumen.MeterChanges,
                Previewer.CurrentPlayTime,
                Previewer.CurrentPlayTime + Previewer.ViewHeight,
                0,
                1,
                1
            );

            var transDisp = Previewer.ViewWidth * 0.4f;
            var maxDispAlpha = 0.3f;
            var eDisp = Previewer.ViewWidth - transDisp;

            foreach ((var t, var y, _) in timelines.Where(x => x.beatIndex == 0))
            {
                drawLines.Add(new()
                {
                    TGrid = t,
                    Y = y
                });

                list.Add(new(new(0, (float)y), new(1, 1, 1, 0f)));
                list.Add(new(new(0, (float)y), new(1, 1, 1, maxDispAlpha)));
                list.Add(new(new(transDisp, (float)y), new(1, 1, 1, 0f)));
                list.Add(new(new(eDisp, (float)y), new(1, 1, 1, 0f)));
                list.Add(new(new(Previewer.ViewWidth, (float)y), new(1, 1, 1, maxDispAlpha)));
                list.Add(new(new(Previewer.ViewWidth, (float)y), new(1, 1, 1, 0f)));
            }
        }

        public void Draw(OngekiFumen fumen)
        {
            Draw(default, fumen);
        }

        public void DrawTimeSigntureText(DrawStringHelper stringHelper)
        {
            stringHelper.Begin(Previewer);
            foreach (var pair in drawLines)
                stringHelper.Draw(pair.TGrid.ToString(), new(-Previewer.ViewWidth / 2, (float)pair.Y + 10), System.Numerics.Vector2.One, 0, 12, new(0, 0.5f));
            stringHelper.End();
        }
    }
}
