using Caliburn.Micro;
using OngekiFumenEditor.Kernel.Graphics;
using OpenTK.Graphics.OpenGL;
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

        private ISimpleLineDrawing lineDrawing;
        private IPolygonDrawing polygonDrawing;

        private VertexDash dash = new()
        {
            DashSize = 8,
            GapSize = 4
        };

        public DrawSelectingRangeHelper()
        {
            lineDrawing = IoC.Get<ISimpleLineDrawing>();
            polygonDrawing = IoC.Get<IPolygonDrawing>();
        }

        public void Draw(IFumenEditorDrawingContext target)
        {
            if (target.Editor.SelectionArea is not { } selectionArea || selectionArea.Rect is { Height: 0, Width: 0 } || !selectionArea.IsActive)
                return;

            RangeColors colors;
            if (selectionArea.SelectionAreaKind == SelectionAreaKind.Select) {
                if (selectionArea.FilterFunc is not null) {
                    colors = SelectFiltered;
                }
                else {
                    colors = SelectAll;
                }
            } else if (selectionArea.SelectionAreaKind == SelectionAreaKind.Delete) {
                if (selectionArea.FilterFunc is not null) {
                    colors = DeleteFiltered;
                }
                else {
                    colors = DeleteAll;
                }
            }
            else {
                colors = SelectAll;
            }

            var topY = (float)selectionArea.Rect.Top;
            var buttomY = (float)selectionArea.Rect.Bottom;
            var rightX = (float)selectionArea.Rect.Right;
            var leftX = (float)selectionArea.Rect.Left;
            var centerX = (leftX + rightX) / 2;
            var centerY = (topY + buttomY) / 2;

            polygonDrawing.Begin(target, PrimitiveType.TriangleStrip);
            {
                polygonDrawing.PostPoint(new(leftX, buttomY), colors.RectColor);
                polygonDrawing.PostPoint(new(centerX, centerY), colors.RectColor);
                polygonDrawing.PostPoint(new(leftX, topY), colors.RectColor);
                polygonDrawing.PostPoint(new(centerX, centerY), colors.RectColor);
                polygonDrawing.PostPoint(new(rightX, topY), colors.RectColor);
                polygonDrawing.PostPoint(new(centerX, centerY), colors.RectColor);
                polygonDrawing.PostPoint(new(rightX, buttomY), colors.RectColor);
                polygonDrawing.PostPoint(new(centerX, centerY), colors.RectColor);
                polygonDrawing.PostPoint(new(leftX, buttomY), colors.RectColor);
            }
            polygonDrawing.End();

            lineDrawing.Begin(target, 1);
            {
                lineDrawing.PostPoint(new(leftX, buttomY), colors.LineColor, dash);
                lineDrawing.PostPoint(new(leftX, topY), colors.LineColor, dash);
                lineDrawing.PostPoint(new(rightX, topY), colors.LineColor, dash);
                lineDrawing.PostPoint(new(rightX, buttomY), colors.LineColor, dash);
                lineDrawing.PostPoint(new(leftX, buttomY), colors.LineColor, dash);
            }
            lineDrawing.End();
        }
    }
}