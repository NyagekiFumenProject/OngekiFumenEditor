using System.Collections.Generic;
using Injectio.Attributes;

namespace OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Graphics.Drawing.TargetImpl.EditorObjects.Lane
{
    [RegisterSingleton<IFumenEditorDrawingTarget>]
    internal class BeamEditorObjectDrawingTarget : TextureLaneEditorObjectDrawingTarget
    {
        public override IEnumerable<string> DrawTargetID { get; } = new[]
        {
            "BMS","OBS"
        };

        public BeamEditorObjectDrawingTarget() : base(
            "laneStart.png",
            "laneNext.png",
            "laneEnd.png"
            )
        {

        }
    }
}


