using Caliburn.Micro;
using OngekiFumenEditor.Core.Base.OngekiObjects;
using OngekiFumenEditor.Kernel.Graphics;
using OngekiFumenEditor.Kernel.Graphics.DrawCommands;
using System.Collections.Generic;
using System.ComponentModel.Composition;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Graphics.Drawing.TargetImpl.OngekiObjects.Holds
{
    [Export(typeof(IFumenEditorDrawingTarget))]
    internal sealed class HoldTapDrawingTarget : CommonDrawTargetBase<Hold>
    {
        public override IEnumerable<string> DrawTargetID { get; } = new string[] { "HLD", "CHD", "XHD" };

        public override int DefaultRenderOrder => 1200;

        private TapDrawingTarget tapDraw;

        public override void Initialize(IRenderManagerImpl impl)
        {
            tapDraw = new TapDrawingTarget();
            tapDraw.Initialize(impl);
        }

        public override void Draw(IFumenEditorDrawingContext target, IDrawCommandListBuilder builder, Hold hold)
        {
            var start = hold.ReferenceLaneStart;
            var holdEnd = hold.HoldEnd;
            var laneType = start?.LaneType;
            var soflanGroup = target.Editor._cacheSoflanGroupRecorder.GetCache(hold);

            //draw taps
            if (target.CheckDrawingVisible(tapDraw.Visible))
            {
                tapDraw.Begin(target, builder);
                tapDraw.Draw(target, builder, laneType, hold, hold.IsCritical, soflanGroup);
                if (holdEnd != null)
                    tapDraw.Draw(target, builder, laneType, holdEnd, false, soflanGroup);
                tapDraw.End();
            }
        }
    }
}
