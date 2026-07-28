using System.Collections.Generic;
using Injectio.Attributes;

namespace OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Graphics.Drawing.TargetImpl.EditorObjects.Lane
{
    [RegisterSingleton<IFumenEditorDrawingTarget>]
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


