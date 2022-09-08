using Caliburn.Micro;
using FontStashSharp;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Modules.FumenPreviewer.Graphics.Drawing.Base;
using OngekiFumenEditor.Modules.FumenVisualEditor;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
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
        private IStringDrawing stringDrawing;

        public DrawTimeSignatureHelper()
        {
            stringDrawing = IoC.Get<IStringDrawing>();
        }

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

        public void DrawTimeSigntureText()
        {
            foreach (var pair in drawLines)
                stringDrawing.Draw(
                    pair.TGrid.ToString(),
                    new(Previewer.ViewWidth / 2,
                    (float)pair.Y + 10),
                    Vector2.One,
                    12,
                    0,
                    Vector4.One,
                    new(1, 0.5f),
                    IStringDrawing.StringStyle.Normal,
                    Previewer,
                    default,
                    out _
            );
        }
    }
}
