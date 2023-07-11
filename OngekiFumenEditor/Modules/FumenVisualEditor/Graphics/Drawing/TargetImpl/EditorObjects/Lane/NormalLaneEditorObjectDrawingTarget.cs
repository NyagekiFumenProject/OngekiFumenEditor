using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OngekiFumenEditor.Kernel.Graphics;

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
            LoadTextrueFromDefaultResource("NS.png"),
            LoadTextrueFromDefaultResource("NN.png"),
            LoadTextrueFromDefaultResource("NE.png")
            )
        {
        }
    }
}
