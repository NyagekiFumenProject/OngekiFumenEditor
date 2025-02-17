using System.Collections.Generic;
using System.ComponentModel.Composition;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Graphics.Drawing.TargetImpl.EditorObjects.Lane
{
    [Export(typeof(IFumenEditorDrawingTarget))]
    internal class BeamEditorObjectDrawingTarget : TextureLaneEditorObjectDrawingTarget
    {
        public override IEnumerable<string> DrawTargetID { get; } = new[]
        {
            "BMS","OBS"
        };

        public BeamEditorObjectDrawingTarget() : base(
            LoadTextrueFromDefaultResource("laneStart.png"),
            LoadTextrueFromDefaultResource("laneNext.png"),
            LoadTextrueFromDefaultResource("laneEnd.png")
            )
        {

        }
    }
}
