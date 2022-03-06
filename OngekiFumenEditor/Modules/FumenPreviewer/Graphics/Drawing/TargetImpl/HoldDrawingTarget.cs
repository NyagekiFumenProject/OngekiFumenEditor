using Caliburn.Micro;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Base.OngekiObjects.Lane.Base;
using OngekiFumenEditor.Modules.FumenPreviewer.Graphics.PrimitiveValue;
using OngekiFumenEditor.Modules.FumenVisualEditor;
using OngekiFumenEditor.Utils;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Modules.FumenPreviewer.Graphics.Drawing.TargetImpl
{
    [Export(typeof(IDrawingTarget))]
    public class HoldDrawingTarget : CommonDrawTargetBase<Hold>
    {
        public override IEnumerable<string> DrawTargetID { get; } = new string[] { /*"HLD", "CHD", "XHD"*/ };

        private class HoldLinesDrawHelper : CommonLinesDrawTargetBase<LaneStartBase>
        {
            public override IEnumerable<string> DrawTargetID => default;

            public List<LinePoint> CurrentPoints { get; } = new();

            public override void FillLine(List<LinePoint> list, LaneStartBase obj, OngekiFumen fumen)
            {
                list.AddRange(CurrentPoints);
            }
        }

        private HoldLinesDrawHelper lineDraw = new();
        private TapDrawingTarget tapDraw;

        public HoldDrawingTarget() : base()
        {
            tapDraw = IoC.Get<TapDrawingTarget>();
        }

        public override void Draw(Hold hold, OngekiFumen fumen)
        {
            if (hold.Children.FirstOrDefault() is not HoldEnd holdEnd)
                return;
            //draw line


            //draw taps
            tapDraw.BeginDraw();
            tapDraw.Draw(hold.ReferenceLaneStart?.LaneType, hold.TGrid, hold.XGrid, fumen);
            tapDraw.Draw(hold.ReferenceLaneStart?.LaneType, holdEnd.TGrid, holdEnd.XGrid, fumen);
            tapDraw.EndDraw();
        }
    }
}
