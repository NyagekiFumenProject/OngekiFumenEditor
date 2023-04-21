using Caliburn.Micro;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Utils.ObjectPool;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using static OngekiFumenEditor.Modules.FumenVisualEditor.Graphics.Drawing.ILineDrawing;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Graphics.Drawing.TargetImpl.OngekiObjects.Holds
{
    [Export(typeof(IDrawingTarget))]
    internal class HoldTapDrawingTarget : CommonDrawTargetBase<Hold>
    {
        public override IEnumerable<string> DrawTargetID { get; } = new string[] { "HLD", "CHD", "XHD" };

        public override int DefaultRenderOrder => 1200;

        private TapDrawingTarget tapDraw;

        public HoldTapDrawingTarget() : base()
        {
            tapDraw = IoC.Get<TapDrawingTarget>();
        }

        public override void Draw(IFumenEditorDrawingContext target, Hold hold)
        {
            var start = hold.ReferenceLaneStart;
            var holdEnd = hold.HoldEnd;
            var laneType = start?.LaneType;

            //draw taps
            tapDraw.Begin(target);
            tapDraw.Draw(target, laneType, hold, hold.IsCritical);
            if (holdEnd != null)
                tapDraw.Draw(target, laneType, holdEnd, false);
            tapDraw.End();
        }
    }
}
