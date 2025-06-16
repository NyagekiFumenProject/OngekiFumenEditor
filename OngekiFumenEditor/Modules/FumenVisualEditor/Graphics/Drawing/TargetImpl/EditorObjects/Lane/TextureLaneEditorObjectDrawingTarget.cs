using OngekiFumenEditor.Kernel.Graphics;
using OngekiFumenEditor.Utils;
using System;
using System.Drawing;
using System.IO;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Graphics.Drawing.TargetImpl.EditorObjects.Lane
{
    public abstract class TextureLaneEditorObjectDrawingTarget : CommonLaneEditorObjectDrawingTarget
    {
        public static IImage LoadTextrueFromDefaultResource(string rPath)
        {
            return ResourceUtils.OpenReadTextureFromFile(@".\Resources\editor\" + rPath);
        }

        public override IImage StartEditorTexture { get; }
        public override IImage NextEditorTexture { get; }
        public override IImage EndEditorTexture { get; }

        public TextureLaneEditorObjectDrawingTarget(IImage startEditorTexture, IImage nextEditorTexture, IImage endEditorTexture)
        {
            StartEditorTexture = startEditorTexture;
            NextEditorTexture = nextEditorTexture;
            EndEditorTexture = endEditorTexture;
        }
    }
}
