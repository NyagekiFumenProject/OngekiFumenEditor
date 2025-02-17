using Caliburn.Micro;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Kernel.Graphics;
using OngekiFumenEditor.Kernel.Graphics.Base;
using OngekiFumenEditor.Utils;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Numerics;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Graphics.Drawing.TargetImpl.OngekiObjects
{
    [Export(typeof(IFumenEditorDrawingTarget))]
    public class FlickDrawingTarget : CommonBatchDrawTargetBase<Flick>, IDisposable
    {
        public override int DefaultRenderOrder => 1000;

        private Texture texture;
        private Texture exFlickEffTexture;

        private Vector2 leftSize;
        private Vector2 rightSize;
        private Vector2 exTapEffSize;
        private Vector2 selectedEffSize;

        private List<(Vector2, Vector2, float)> exFlickList = new();
        private List<(Vector2, Vector2, float)> selectedFlickList = new();
        private List<(Vector2, Vector2, float)> normalFlichList = new();

        private IBatchTextureDrawing batchTextureDrawing;
        private IHighlightBatchTextureDrawing highlightDrawing;

        public override IEnumerable<string> DrawTargetID { get; } = new[] { "FLK", "CFK" };

        public FlickDrawingTarget() : base()
        {
            texture = ResourceUtils.OpenReadTextureFromFile(@".\Resources\editor\flick.png");
            exFlickEffTexture = ResourceUtils.OpenReadTextureFromFile(@".\Resources\editor\exflickEffect.png");

            if (!ResourceUtils.OpenReadTextureSizeAnchorByConfigFile("flick", out var size, out _))
                size = new Vector2(104, 69.333f);
            leftSize = size;
            rightSize = size * new Vector2(-1, 1);

            if (!ResourceUtils.OpenReadTextureSizeAnchorByConfigFile("exflickEffect", out size, out _))
                size = new Vector2(106, 67f);
            exTapEffSize = size;
            selectedEffSize = size * 1.05f;

            batchTextureDrawing = IoC.Get<IBatchTextureDrawing>();
            highlightDrawing = IoC.Get<IHighlightBatchTextureDrawing>();
        }

        public override void DrawBatch(IFumenEditorDrawingContext target, IEnumerable<Flick> objs)
        {
            foreach (var obj in objs)
            {
                var x = XGridCalculator.ConvertXGridToX(obj.XGrid, target.Editor);
                var y = target.ConvertToY(obj.TGrid) + 24;
                var pos = new Vector2((float)x, (float)y);
                var size = obj.Direction == Flick.FlickDirection.Right ? rightSize : leftSize;
                normalFlichList.Add((size, pos, 0f));

                if (obj.IsCritical)
                {
                    var exTapSize = exTapEffSize;
                    exTapSize.X = Math.Sign(size.X) * exTapSize.X;
                    pos.Y -= 1;

                    exFlickList.Add((exTapSize, pos, 0));
                }

                if (obj.IsSelected)
                {
                    var selectTapSize = selectedEffSize;
                    selectTapSize.X = Math.Sign(size.X) * selectTapSize.X;
                    pos.Y -= 1;

                    selectedFlickList.Add((selectTapSize, pos, 0));
                }

                size.X = Math.Abs(size.X);
                target.RegisterSelectableObject(obj, pos, size);
            }

            highlightDrawing.Draw(target, texture, selectedFlickList);
            batchTextureDrawing.Draw(target, texture, normalFlichList);
            batchTextureDrawing.Draw(target, exFlickEffTexture, exFlickList);

            exFlickList.Clear();
            selectedFlickList.Clear();
            normalFlichList.Clear();
        }

        public void Dispose()
        {
            texture?.Dispose();
            texture = null;
        }
    }
}
