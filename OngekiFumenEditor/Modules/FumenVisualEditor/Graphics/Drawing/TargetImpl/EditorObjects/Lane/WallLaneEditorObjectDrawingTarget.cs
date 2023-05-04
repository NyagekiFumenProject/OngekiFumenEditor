using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Drawing;
using OngekiFumenEditor.Kernel.Graphics;

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
            LoadTextrueFromDefaultResource("WS.png"),
            LoadTextrueFromDefaultResource("WN.png"),
            LoadTextrueFromDefaultResource("WE.png")
            )
        {
        }
    }
}
