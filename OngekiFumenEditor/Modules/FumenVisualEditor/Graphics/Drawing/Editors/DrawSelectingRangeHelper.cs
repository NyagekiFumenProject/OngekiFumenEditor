using Caliburn.Micro;
using OngekiFumenEditor.Kernel.Graphics;
using OpenTK.Graphics.OpenGL;
using System;
using System.Numerics;
using static OngekiFumenEditor.Kernel.Graphics.ILineDrawing;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Graphics.Drawing.Editors
{
    public class DrawSelectingRangeHelper
    {
        private ISimpleLineDrawing lineDrawing;
        private IPolygonDrawing polygonDrawing;

        private Vector4 lineColor = new(1, 0, 1, 1);
        private Vector4 fillColor = new(1, 1, 1, 0.15f);
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
            if (target.Editor.SelectionVisibility != System.Windows.Visibility.Visible)
                return;

            var topY = Math.Max(target.Editor.SelectionCurrentCursorPosition.Y, target.Editor.SelectionStartPosition.Y);
            var buttomY = Math.Min(target.Editor.SelectionCurrentCursorPosition.Y, target.Editor.SelectionStartPosition.Y);
            var rightX = Math.Max(target.Editor.SelectionCurrentCursorPosition.X, target.Editor.SelectionStartPosition.X);
            var leftX = Math.Min(target.Editor.SelectionCurrentCursorPosition.X, target.Editor.SelectionStartPosition.X);
            var centerX = (leftX + rightX) / 2;
            var centerY = (topY + buttomY) / 2;

            polygonDrawing.Begin(target, PrimitiveType.TriangleStrip);
            {
                polygonDrawing.PostPoint(new(leftX, buttomY), fillColor);
                polygonDrawing.PostPoint(new(centerX, centerY), fillColor);
                polygonDrawing.PostPoint(new(leftX, topY), fillColor);
                polygonDrawing.PostPoint(new(centerX, centerY), fillColor);
                polygonDrawing.PostPoint(new(rightX, topY), fillColor);
                polygonDrawing.PostPoint(new(centerX, centerY), fillColor);
                polygonDrawing.PostPoint(new(rightX, buttomY), fillColor);
                polygonDrawing.PostPoint(new(centerX, centerY), fillColor);
                polygonDrawing.PostPoint(new(leftX, buttomY), fillColor);
            }
            polygonDrawing.End();

            lineDrawing.Begin(target, 1);
            {
                lineDrawing.PostPoint(new(leftX, buttomY), lineColor, dash);
                lineDrawing.PostPoint(new(leftX, topY), lineColor, dash);
                lineDrawing.PostPoint(new(rightX, topY), lineColor, dash);
                lineDrawing.PostPoint(new(rightX, buttomY), lineColor, dash);
                lineDrawing.PostPoint(new(leftX, buttomY), lineColor, dash);
            }
            lineDrawing.End();
        }
    }
}
