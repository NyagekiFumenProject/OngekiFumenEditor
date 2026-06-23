using Caliburn.Micro;
using OngekiFumenEditor.Kernel.Graphics;
using OngekiFumenEditor.Kernel.Graphics.DrawCommands;
using OngekiFumenEditor.Kernel.Graphics.DrawCommands.DefaultDrawCommands;
using System;
using System.Collections.Generic;
using System.Numerics;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using OngekiFumenEditor.Utils;
using static OngekiFumenEditor.Kernel.Graphics.ILineDrawing;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Graphics.Drawing.Editors
{
    public class DrawSelectingRangeHelper
    {
        private record RangeColors(Vector4 LineColor, Vector4 RectColor)
        {
            public readonly Vector4 LineColor = LineColor;
            public readonly Vector4 RectColor = RectColor;
        }

        private RangeColors SelectAll = new(new(1, 0, 1, 1), new(1, 1, 1, 0.15f));
        private RangeColors SelectFiltered = new(new(1, 0, 1, 1), new(0.8f, 0.0f, 0.8f, 0.15f));
        private RangeColors DeleteAll = new(new(1, 0.1f, 0.1f, 1), new(1, 0.1f, 0.1f, 0.15f));
        private RangeColors DeleteFiltered = new(new(1, 0.1f, 0.1f, 1), new(0.8f, 0.8f, 0, 0.15f));

        private VertexDash dash = new(8,4);

        public void Initalize(IRenderManagerImpl impl)
        {
        }

        public void Draw(IFumenEditorDrawingContext target, IDrawCommandListBuilder builder)
        {
            if (target.Editor.SelectionArea is not { } selectionArea || selectionArea.Rect is { Height: 0, Width: 0 } || !selectionArea.IsActive)
                return;

            RangeColors colors;
            if (selectionArea.SelectionAreaKind == SelectionAreaKind.Select)
            {
                if (selectionArea.FilterFunc is not null)
                {
                    colors = SelectFiltered;
                }
                else
                {
                    colors = SelectAll;
                }
            }
            else if (selectionArea.SelectionAreaKind == SelectionAreaKind.Delete)
            {
                if (selectionArea.FilterFunc is not null)
                {
                    colors = DeleteFiltered;
                }
                else
                {
                    colors = DeleteAll;
                }
            }
            else
            {
                colors = SelectAll;
            }

            var originY = target.CurrentDrawingTargetContext?.ViewRelativeOriginY ?? 0;
            var topY = (float)(selectionArea.Rect.Top - originY);
            var buttomY = (float)(selectionArea.Rect.Bottom - originY);
            var rightX = (float)selectionArea.Rect.Right;
            var leftX = (float)selectionArea.Rect.Left;
            var centerX = (leftX + rightX) / 2;
            var centerY = (topY + buttomY) / 2;

            builder.DrawPolygon(Primitive.TriangleStrip, new[]
            {
                new PolygonVertex(new(leftX, buttomY), colors.RectColor),
                new PolygonVertex(new(centerX, centerY), colors.RectColor),
                new PolygonVertex(new(leftX, topY), colors.RectColor),
                new PolygonVertex(new(centerX, centerY), colors.RectColor),
                new PolygonVertex(new(rightX, topY), colors.RectColor),
                new PolygonVertex(new(centerX, centerY), colors.RectColor),
                new PolygonVertex(new(rightX, buttomY), colors.RectColor),
                new PolygonVertex(new(centerX, centerY), colors.RectColor),
                new PolygonVertex(new(leftX, buttomY), colors.RectColor),
            });

            builder.DrawSimpleLines(new[]
            {
                new LineVertex(new(leftX, buttomY), colors.LineColor, dash),
                new LineVertex(new(leftX, topY), colors.LineColor, dash),
                new LineVertex(new(rightX, topY), colors.LineColor, dash),
                new LineVertex(new(rightX, buttomY), colors.LineColor, dash),
                new LineVertex(new(leftX, buttomY), colors.LineColor, dash),
            }, 1);
        }
    }
}
