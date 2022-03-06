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
        public override IEnumerable<string> DrawTargetID => throw new NotImplementedException();

        public override void FillLine(List<LinePoint> list, OngekiObjectBase obj, OngekiFumen fumen)
        {
            var timelines = TGridCalculator.GetVisbleTimelines(
                fumen.BpmList,
                fumen.MeterChanges,
                Previewer.CurrentPlayTime,
                Previewer.CurrentPlayTime + Previewer.ViewHeight,
                0,
                1,
                240
            );

            var transDisp = Previewer.ViewWidth * 0.4f;
            var maxDispAlpha = 0.3f;
            var eDisp = Previewer.ViewWidth - transDisp;

            foreach ((_, var y, _) in timelines.Where(x => x.beatIndex == 0))
            {
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
            BeginDraw();
            Draw(default, fumen);
            EndDraw();
        }
    }
}
