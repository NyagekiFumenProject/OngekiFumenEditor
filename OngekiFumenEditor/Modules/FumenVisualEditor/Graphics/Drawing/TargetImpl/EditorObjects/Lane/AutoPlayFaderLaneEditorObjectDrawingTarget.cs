﻿using System.Collections.Generic;
using System.ComponentModel.Composition;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Graphics.Drawing.TargetImpl.EditorObjects.Lane
{
    [Export(typeof(IFumenEditorDrawingTarget))]
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
