using Caliburn.Micro;
using OngekiFumenEditor.Base.EditorObjects.Svg;
using OngekiFumenEditor.Modules.FumenPreviewer.Graphics.Drawing.TargetImpl.EditorObjects.SVG.Cached;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using OngekiFumenEditor.Modules.FumenVisualEditor;
using OpenTK.Mathematics;
using System.Windows;
using static OngekiFumenEditor.Modules.FumenPreviewer.Graphics.Drawing.ISimpleLineDrawing;
using System;
using System.Net.Sockets;
using static OngekiFumenEditor.Modules.FumenPreviewer.Graphics.Drawing.IStaticVBODrawing;

namespace OngekiFumenEditor.Modules.FumenPreviewer.Graphics.Drawing.TargetImpl.EditorObjects
{
    [Export(typeof(IDrawingTarget))]
    public class SvgObjectDrawingTarget : CommonDrawTargetBase<SvgPrefabBase>, IDisposable
    {
        private ICachedSvgRenderDataManager cachedSvgRenderDataManager;
        private ISimpleLineDrawing lineDrawing;
        private Dictionary<SvgPrefabBase, IVBOHandle> vboHolder = new();

        public override IEnumerable<string> DrawTargetID { get; } = new[] { SvgStringPrefab.CommandName, SvgImageFilePrefab.CommandName };

        public SvgObjectDrawingTarget()
        {
            cachedSvgRenderDataManager = IoC.Get<ICachedSvgRenderDataManager>();
            lineDrawing = IoC.Get<ISimpleLineDrawing>();
        }

        public override void Draw(IFumenPreviewer target, SvgPrefabBase obj)
        {
            var vertics = cachedSvgRenderDataManager.GetRenderData(target, obj, out var isCached, out var bound);
            if (vertics.Count == 0)
                return;
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

            var x = (float)(XGridCalculator.ConvertXGridToX(obj.XGrid, 30, target.ViewWidth, 1) + w / 2);
            var y = (float)(TGridCalculator.ConvertTGridToY(obj.TGrid, target.Fumen.BpmList, 1.0, 240) - h / 2);


            lineDrawing.PushOverrideModelMatrix(lineDrawing.GetOverrideModelMatrix() * Matrix4.CreateTranslation(x, y, 0));
            {
                lineDrawing.DrawVBO(target, handle);
            }
            lineDrawing.PopOverrideModelMatrix(out _);
        }

        public void Dispose()
        {
            foreach (var item in vboHolder.Values)
                item.Dispose();

            vboHolder.Clear();
        }
    }
}
