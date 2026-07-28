using System.Collections.Generic;
using Injectio.Attributes;

namespace OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Graphics.Drawing.TargetImpl.EditorObjects.Lane
{
    [RegisterSingleton<IFumenEditorDrawingTarget>]
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


