using System.Collections.Generic;
using Injectio.Attributes;

namespace OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Graphics.Drawing.TargetImpl.EditorObjects.Lane
{
    [RegisterSingleton<IFumenEditorDrawingTarget>]
    internal class AutoPlayFaderLaneEditorObjectDrawingTarget : TextureLaneEditorObjectDrawingTarget
    {
        public override IEnumerable<string> DrawTargetID { get; } = new[]
        {
            "[APFS]",
        };

        public AutoPlayFaderLaneEditorObjectDrawingTarget() : base(
            "laneStart.png",
            "laneNext.png",
            "laneEnd.png"
            )
        {
        }
    }
}


