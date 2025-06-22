using OngekiFumenEditor.Kernel.Graphics;
using OngekiFumenEditor.Utils;
using System;
using System.Drawing;
using System.IO;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Graphics.Drawing.TargetImpl.EditorObjects.Lane
{
    public abstract class TextureLaneEditorObjectDrawingTarget : CommonLaneEditorObjectDrawingTarget, IDisposable
    {
        private string startEditorTextureName;
        private string nextEditorTextureName;
        private string endEditorTextureName;
        private IImage startEditorTexture;
        private IImage nextEditorTexture;
        private IImage endEditorTexture;

        public static IImage LoadTextrueFromDefaultResource(IRenderManagerImpl impl, string rPath)
        {
            return ResourceUtils.OpenReadTextureFromFile(impl, @".\Resources\editor\" + rPath);
        }

        public override IImage StartEditorTexture => startEditorTexture;
        public override IImage NextEditorTexture => nextEditorTexture;
        public override IImage EndEditorTexture => endEditorTexture;

        public override void Initialize(IRenderManagerImpl impl)
        {
            base.Initialize(impl);

            startEditorTexture = LoadTextrueFromDefaultResource(impl, startEditorTextureName);
            nextEditorTexture = LoadTextrueFromDefaultResource(impl, nextEditorTextureName);
            endEditorTexture = LoadTextrueFromDefaultResource(impl, endEditorTextureName);
        }

        public void Dispose()
        {
            startEditorTexture.Dispose();
            nextEditorTexture.Dispose();
            endEditorTexture.Dispose();
        }

        public TextureLaneEditorObjectDrawingTarget(string startEditorTextureName, string nextEditorTextureName, string endEditorTextureName)
        {
            this.startEditorTextureName = startEditorTextureName;
            this.nextEditorTextureName = nextEditorTextureName;
            this.endEditorTextureName = endEditorTextureName;
        }
    }
}
