using System.Collections.Generic;
using System.ComponentModel.Composition;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Graphics.Drawing.TargetImpl.EditorObjects.Lane
{
    [Export(typeof(IFumenEditorDrawingTarget))]
    internal class WallLaneEditorObjectDrawingTarget : TextureLaneEditorObjectDrawingTarget
    {
        public override IEnumerable<string> DrawTargetID { get; } = new[]
        {
            "WLS","WRS"
        };

        public WallLaneEditorObjectDrawingTarget() : base(
            "wallStart.png",
            "wallNext.png",
            "wallEnd.png"
            )
        {
        }
    }
}
