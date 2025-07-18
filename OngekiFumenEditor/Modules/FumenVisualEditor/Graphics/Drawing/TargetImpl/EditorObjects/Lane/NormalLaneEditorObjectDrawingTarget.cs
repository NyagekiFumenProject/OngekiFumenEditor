﻿using System.Collections.Generic;
using System.ComponentModel.Composition;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Graphics.Drawing.TargetImpl.EditorObjects.Lane
{
    [Export(typeof(IFumenEditorDrawingTarget))]
    internal class NormalLaneEditorObjectDrawingTarget : TextureLaneEditorObjectDrawingTarget
    {
        public override IEnumerable<string> DrawTargetID { get; } = new[]
        {
            "LLS","LCS","LRS","CLS","ENS"
        };

        public NormalLaneEditorObjectDrawingTarget() : base(
            "laneStart.png",
            "laneNext.png",
            "laneEnd.png"
            )
        {
        }
    }
}
