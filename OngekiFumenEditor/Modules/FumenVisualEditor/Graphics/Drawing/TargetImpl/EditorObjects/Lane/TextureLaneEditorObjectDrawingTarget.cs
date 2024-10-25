using OngekiFumenEditor.Kernel.Graphics.Base;
using OngekiFumenEditor.Utils;
using System;
using System.Drawing;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Graphics.Drawing.TargetImpl.EditorObjects.Lane
{
    public abstract class TextureLaneEditorObjectDrawingTarget : CommonLaneEditorObjectDrawingTarget
    {
        public static Texture LoadTextrueFromDefaultResource(string rPath)
        {
            var texture = ResourceUtils.OpenReadTextureFromFile(@".\Resources\editor\" + rPath);
            return texture;
        }

        public override Texture StartEditorTexture { get; }
        public override Texture NextEditorTexture { get; }
        public override Texture EndEditorTexture { get; }

        public TextureLaneEditorObjectDrawingTarget(Texture startEditorTexture, Texture nextEditorTexture, Texture endEditorTexture)
        {
            StartEditorTexture = startEditorTexture;
            NextEditorTexture = nextEditorTexture;
            EndEditorTexture = endEditorTexture;
        }
    }
}
