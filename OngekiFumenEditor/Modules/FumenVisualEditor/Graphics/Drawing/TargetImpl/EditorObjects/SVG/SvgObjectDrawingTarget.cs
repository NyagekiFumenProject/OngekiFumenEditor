using Caliburn.Micro;
using OngekiFumenEditor.Base.EditorObjects.Svg;
using OngekiFumenEditor.Modules.FumenVisualEditor.Graphics.Drawing.TargetImpl.EditorObjects.SVG.Cached;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using OngekiFumenEditor.Modules.FumenVisualEditor;
using System.Windows;
using static OngekiFumenEditor.Modules.FumenVisualEditor.Graphics.Drawing.ISimpleLineDrawing;
using System;
using System.Net.Sockets;
using static OngekiFumenEditor.Modules.FumenVisualEditor.Graphics.Drawing.IStaticVBODrawing;
using System.Drawing;
using OngekiFumenEditor.Utils;
using System.Numerics;
using NAudio.Gui;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Graphics.Drawing.TargetImpl.EditorObjects.SVG
{
    [Export(typeof(IDrawingTarget))]
    public class SvgObjectDrawingTarget : CommonDrawTargetBase<SvgPrefabBase>, IDisposable
    {
        private Texture texture;
        private ICachedSvgRenderDataManager cachedSvgRenderDataManager;
        private ISimpleLineDrawing lineDrawing;
        private ITextureDrawing textureDrawing;
        private IHighlightBatchTextureDrawing highlightDrawing;
        private Dictionary<SvgPrefabBase, IVBOHandle> vboHolder = new();

        public override IEnumerable<string> DrawTargetID { get; } = new[] { SvgStringPrefab.CommandName, SvgImageFilePrefab.CommandName };

        public override int DefaultRenderOrder => 1000 + 0;

        public SvgObjectDrawingTarget()
        {
            texture = ResourceUtils.OpenReadTextureFromResource(@"Modules\FumenVisualEditor\Views\OngekiObjects\CC.png");

            cachedSvgRenderDataManager = IoC.Get<ICachedSvgRenderDataManager>();
            lineDrawing = IoC.Get<ISimpleLineDrawing>();
            textureDrawing = IoC.Get<ITextureDrawing>();
            highlightDrawing = IoC.Get<IHighlightBatchTextureDrawing>();
        }

        public override void Draw(IFumenEditorDrawingContext target, SvgPrefabBase obj)
        {
            var x = (float)XGridCalculator.ConvertXGridToX(obj.XGrid, target.Editor);
            var y = (float)TGridCalculator.ConvertTGridToY(obj.TGrid, target.Editor);
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

                var w = bound.Width;
                var h = bound.Height;


                var dx = x + w / 2;
                var dy = y - h / 2;
                lineDrawing.PushOverrideModelMatrix(lineDrawing.GetOverrideModelMatrix() * OpenTK.Mathematics.Matrix4.CreateTranslation((float)dx, (float)dy, 0));
                {
                    lineDrawing.DrawVBO(target, handle);
                }
                lineDrawing.PopOverrideModelMatrix(out _);
            }

            if (obj.IsSelected)
                highlightDrawing.Draw(target, texture, new[] { (new Vector2(20, 20), pos, 0f) });
            textureDrawing.Draw(target, texture, new[] { (new Vector2(16, 16), pos, 0f) });
            target.RegisterSelectableObject(obj, pos, new(16, 16));
        }

        public void Dispose()
        {
            foreach (var item in vboHolder.Values)
                item.Dispose();

            vboHolder.Clear();
        }
    }
}
