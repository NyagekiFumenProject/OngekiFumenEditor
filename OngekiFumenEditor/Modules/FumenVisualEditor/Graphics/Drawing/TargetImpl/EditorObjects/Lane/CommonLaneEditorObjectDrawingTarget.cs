﻿using Caliburn.Micro;
using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Kernel.Graphics;
using OngekiFumenEditor.Utils;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Graphics.Drawing.TargetImpl.EditorObjects.Lane
{
    public abstract class CommonLaneEditorObjectDrawingTarget : CommonBatchDrawTargetBase<ConnectableStartObject>
    {
        public override int DefaultRenderOrder => 2000;
        public override DrawingVisible DefaultVisible => DrawingVisible.Design;

        private IBatchTextureDrawing textureDrawing;
        private IHighlightBatchTextureDrawing highlightDrawing;

        public abstract IImage StartEditorTexture { get; }
        public abstract IImage NextEditorTexture { get; }
        public abstract IImage EndEditorTexture { get; }

        private Vector2 startSize = new(16, 16);
        private Vector2 nextSize = new(16, 16);
        private Vector2 endSize = new(16, 16);
        private List<(Vector2, Vector2, float, Vector4)> selectList = new();
        private List<(Vector2, Vector2, float, Vector4)> drawList = new();

        public override void Initialize(IRenderManagerImpl impl)
        {
            textureDrawing = impl.BatchTextureDrawing;
            highlightDrawing = impl.HighlightBatchTextureDrawing;

            if (!ResourceUtils.OpenReadTextureSizeAnchorByConfigFile("laneStart", out startSize, out _))
                startSize = new Vector2(16, 16);
            if (!ResourceUtils.OpenReadTextureSizeAnchorByConfigFile("laneNext", out nextSize, out _))
                nextSize = new Vector2(16, 16);
            if (!ResourceUtils.OpenReadTextureSizeAnchorByConfigFile("laneEnd", out endSize, out _))
                endSize = new Vector2(16, 16);
        }

        public override void DrawBatch(IFumenEditorDrawingContext target, IEnumerable<ConnectableStartObject> objs)
        {
            target.PerfomenceMonitor.OnBeginTargetDrawing(this);
            {
                void drawEditorTap(IImage texture, Vector2 size, IEnumerable<ConnectableObjectBase> o)
                {
                    foreach (var item in o)
                    {
                        if (!target.CheckVisible(item.TGrid))
                            continue;

                        var x = (float)XGridCalculator.ConvertXGridToX(item.XGrid, target.Editor);
                        var soflanList = target.Editor._cacheSoflanGroupRecorder.GetCache(item);
                        var y = (float)target.ConvertToY(item.TGrid, soflanList);

                        var pos = new Vector2(x, y);
                        drawList.Add((size, pos, 0f, Vector4.One));
                        target.RegisterSelectableObject(item, pos, size);
                        if (item.IsSelected)
                            selectList.Add((size * 1.5f, pos, 0f, Vector4.One));
                    }

                    if (selectList.Count > 0)
                        highlightDrawing.Draw(target, texture, selectList);
                    if (drawList.Count > 0)
                        textureDrawing.Draw(target, texture, drawList);

                    selectList.Clear();
                    drawList.Clear();
                }
                drawEditorTap(StartEditorTexture, startSize, objs);
                drawEditorTap(NextEditorTexture, nextSize, objs.SelectMany(x => x.Children.OfType<ConnectableChildObjectBase>()));
                //drawEditorTap(EndEditorTexture, editorSize, objs.Select(x => x.Children.LastOrDefault()).OfType<ConnectableEndObject>());
            }
            target.PerfomenceMonitor.OnAfterTargetDrawing(this);
        }
    }
}
