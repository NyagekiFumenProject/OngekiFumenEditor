using Caliburn.Micro;
using OngekiFumenEditor.Base.EditorObjects.Svg;
using OngekiFumenEditor.Kernel.Graphics;
using OngekiFumenEditor.Kernel.Graphics.Base;
using OngekiFumenEditor.Modules.FumenVisualEditor.Graphics.Drawing.TargetImpl.EditorObjects.SVG.Cached;
using OngekiFumenEditor.Utils;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Numerics;
using static OngekiFumenEditor.Kernel.Graphics.IStaticVBODrawing;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Graphics.Drawing.TargetImpl.EditorObjects.SVG
{
    [Export(typeof(IFumenEditorDrawingTarget))]
    public class SvgObjectDrawingTarget : CommonDrawTargetBase<SvgPrefabBase>, IDisposable
    {
        private Texture texture;
        private ICachedSvgRenderDataManager cachedSvgRenderDataManager;
        private ISimpleLineDrawing lineDrawing;
        private ITextureDrawing textureDrawing;
        private IHighlightBatchTextureDrawing highlightDrawing;
        private Dictionary<SvgPrefabBase, IVBOHandle> vboHolder = new();
        private Vector2 size;

        public override IEnumerable<string> DrawTargetID { get; } = new[] { SvgStringPrefab.CommandName, SvgImageFilePrefab.CommandName };
        public override DrawingVisible DefaultVisible => DrawingVisible.Design;

        public override int DefaultRenderOrder => 1000 + 0;

        public SvgObjectDrawingTarget()
        {
            texture = ResourceUtils.OpenReadTextureFromFile(@".\Resources\editor\commonCircle.png");
            if (!ResourceUtils.OpenReadTextureSizeAnchorByConfigFile("commonCircle", out size, out _))
                size = new Vector2(16, 16);

            cachedSvgRenderDataManager = IoC.Get<ICachedSvgRenderDataManager>();
            lineDrawing = IoC.Get<ISimpleLineDrawing>();
            textureDrawing = IoC.Get<ITextureDrawing>();
            highlightDrawing = IoC.Get<IHighlightBatchTextureDrawing>();
        }

        public override void Draw(IFumenEditorDrawingContext target, SvgPrefabBase obj)
        {
            var x = (float)XGridCalculator.ConvertXGridToX(obj.XGrid, target.Editor);
            var y = (float)target.ConvertToY(obj.TGrid);
            var pos = new Vector2(x, y);

            var vertics = cachedSvgRenderDataManager.GetRenderData(target, obj, out var isCached, out var bound);
            if (vertics.Count != 0)
            {
                var isCachedVBO = vboHolder.TryGetValue(obj, out var handle);

                if (isCached)
                {
                    if (!isCachedVBO)
                        handle = vboHolder[obj] = lineDrawing.GenerateVBOWithPresetPoints(vertics, 1);
                }
                else
                {
                    handle?.Dispose();
                    handle = vboHolder[obj] = lineDrawing.GenerateVBOWithPresetPoints(vertics, 1);
                }

                var dx = x;
                var dy = y;

                lineDrawing.PushOverrideModelMatrix(lineDrawing.GetOverrideModelMatrix() * OpenTK.Mathematics.Matrix4.CreateTranslation((float)dx, (float)dy, 0));
                {
                    lineDrawing.DrawVBO(target, handle);
                }
                lineDrawing.PopOverrideModelMatrix(out _);
            }

            if (obj.IsSelected)
                highlightDrawing.Draw(target, texture, new[] { (size * 1.25f, pos, 0f) });
            textureDrawing.Draw(target, texture, new[] { (size, pos, 0f) });
            target.RegisterSelectableObject(obj, pos, size);
        }

        public void Dispose()
        {
            foreach (var item in vboHolder.Values)
                item.Dispose();

            vboHolder.Clear();
        }
    }
}
