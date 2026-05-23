using Caliburn.Micro;
using OngekiFumenEditor.Core.Base.EditorObjects.Svg;
using OngekiFumenEditor.Kernel.Graphics;
using OngekiFumenEditor.Kernel.Graphics.DrawCommands;
using OngekiFumenEditor.Modules.FumenVisualEditor.Graphics.Drawing.TargetImpl.EditorObjects.SVG.Cached;
using OngekiFumenEditor.Utils;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Numerics;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Graphics.Drawing.TargetImpl.EditorObjects.SVG
{
    [Export(typeof(IFumenEditorDrawingTarget))]
    public sealed class SvgObjectDrawingTarget : CommonDrawTargetBase<SvgPrefabBase>, IDisposable
    {
        private IImage texture;
        private ICachedSvgRenderDataManager cachedSvgRenderDataManager;
        private Vector2 size;

        public override IEnumerable<string> DrawTargetID { get; } = new[] { SvgStringPrefab.CommandName, SvgImageFilePrefab.CommandName };
        public override DrawingVisible DefaultVisible => DrawingVisible.Design;

        public override int DefaultRenderOrder => 1000 + 0;

        public override void Initialize(IRenderManagerImpl impl)
        {
            texture = ResourceUtils.OpenReadTextureFromFile(impl, @".\Resources\editor\commonCircle.png");
            if (!ResourceUtils.OpenReadTextureSizeAnchorByConfigFile("commonCircle", out size, out _))
                size = new Vector2(16, 16);

            cachedSvgRenderDataManager = IoC.Get<ICachedSvgRenderDataManager>();
        }

        public override void Draw(IFumenEditorDrawingContext target, IDrawCommandListBuilder builder, SvgPrefabBase obj)
        {
            var x = (float)XGridCalculator.ConvertXGridToX(obj.XGrid, target.Editor);
            var soflanList = target.Editor._cacheSoflanGroupRecorder.GetCache(obj);
            var y = (float)target.ConvertToY(obj.TGrid, soflanList);
            var pos = new Vector2(x, y);

            var vertics = cachedSvgRenderDataManager.GetRenderData(target, obj, out var isCached, out var bound);
            if (vertics.Count != 0)
            {
                var dx = x;
                var dy = y;

                builder.PushModelMatrix(Matrix4x4.CreateTranslation((float)dx, (float)dy, 0));
                {
                    builder.DrawSimpleLines(vertics, 1);
                }
                builder.PopModelMatrix();
            }

            if (obj.IsSelected)
                builder.DrawHighlightBatchTexture(texture, new[] { (size * 1.25f, pos, 0f, Vector4.One) });
            builder.DrawTexture(texture, new[] { (size, pos, 0f, Vector4.One) });
            target.RegisterSelectableObject(obj, pos, size);
        }

        public void Dispose()
        {
        }
    }
}
