using Caliburn.Micro;
using Microsoft.VisualBasic;
using NAudio.Gui;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Base.OngekiObjects.Lane.Base;

using OngekiFumenEditor.Modules.FumenVisualEditor;
using OngekiFumenEditor.Utils;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using static OngekiFumenEditor.Modules.FumenVisualEditor.Graphics.Drawing.ILineDrawing;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Graphics.Drawing.TargetImpl
{
    public abstract class LaneDrawingTargetBase<T> : CommonLinesDrawTargetBase<T> where T : ConnectableStartObject
    {
        public override int DefaultRenderOrder => 100;

        private IBatchTextureDrawing textureDrawing;
        private IHighlightBatchTextureDrawing highlightDrawing;

        public Texture StartEditorTexture { get; protected set; }
        public Texture NextEditorTexture { get; protected set; }
        public Texture EndEditorTexture { get; protected set; }

        private Vector2 editorSize = new(16, 16);
        private List<(Vector2, Vector2, float)> selectList = new();
        private List<(Vector2, Vector2, float)> drawList = new();

        public LaneDrawingTargetBase()
        {
            textureDrawing = IoC.Get<IBatchTextureDrawing>();
            highlightDrawing = IoC.Get<IHighlightBatchTextureDrawing>();
        }

        public override void DrawBatch(IFumenEditorDrawingContext target, IEnumerable<T> objs)
        {
            base.DrawBatch(target, objs);
            if (target.Editor.EditorObjectVisibility != System.Windows.Visibility.Visible)
                return;
            target.PerfomenceMonitor.OnBeginTargetDrawing(this);
            {
                var previewMinTGrid = target.Rect.VisiableMinTGrid;
                var previewMaxTGrid = target.Rect.VisiableMaxTGrid;

                void drawEditorTap(Texture texture, Vector2 size, IEnumerable<ConnectableObjectBase> o)
                {
                    foreach (var item in o)
                    {
                        if (item.TGrid < previewMinTGrid || item.TGrid > previewMaxTGrid)
                            continue;

                        var x = (float)XGridCalculator.ConvertXGridToX(item.XGrid, target.Editor);
                        var y = (float)TGridCalculator.ConvertTGridToY(item.TGrid, target.Editor);

                        var pos = new Vector2(x, y);
                        drawList.Add((size, pos, 0f));
                        target.RegisterSelectableObject(item, pos, size);
                        if (item.IsSelected)
                            selectList.Add((size * 1.5f, pos, 0f));
                    }

                    highlightDrawing.Draw(target, texture, selectList);
                    textureDrawing.Draw(target, texture, drawList);

                    selectList.Clear();
                    drawList.Clear();
                }
                drawEditorTap(StartEditorTexture, editorSize, objs);
                drawEditorTap(NextEditorTexture, editorSize, objs.SelectMany(x => x.Children.OfType<ConnectableNextObject>()));
                drawEditorTap(EndEditorTexture, editorSize, objs.Select(x => x.Children.LastOrDefault()).OfType<ConnectableEndObject>());
            }
            target.PerfomenceMonitor.OnAfterTargetDrawing(this);
        }
    }

    public abstract class LaneDrawingTargetBase : LaneDrawingTargetBase<LaneStartBase>
    {

    }
}
