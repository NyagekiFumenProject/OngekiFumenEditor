using Caliburn.Micro;
using OngekiFumenEditor.Kernel.Graphics;
using OngekiFumenEditor.Kernel.Graphics.Text;
using OngekiFumenEditor.Kernel.Graphics.DrawCommands;
using System;
using System.Collections.Generic;
using System.Numerics;
using static OngekiFumenEditor.Kernel.Graphics.ILineDrawing;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Graphics.Drawing.Editors
{
    public class DrawXGridHelper
    {
        public struct CacheDrawXLineResult
        {
            public float X { get; set; }
            public float XGridTotalUnit { get; set; }
            public string XGridTotalUnitDisplay { get; set; }

            public override string ToString() => $"X:{X:F3} XGridTotalUnit:{XGridTotalUnit:F3} Display:{XGridTotalUnitDisplay}";
        }

        public void Initalize(IRenderManagerImpl renderImpl)
        {
        }

        public void DrawLines(IFumenEditorDrawingContext target, IDrawCommandListBuilder builder, IEnumerable<CacheDrawXLineResult> drawLines)
        {
            if (target.Editor.IsPreviewMode)
                return;

            using var list = ObjectPool.GetPooledList<LineVertex>();

            foreach (var result in drawLines)
            {
                var a = Math.Abs(result.XGridTotalUnit) switch
                {
                    0 => 0.4f,
                    24 => 0.6f,
                    _ => 0.25f
                };

                list.Add(new(new(result.X, target.Editor.ViewHeight), new(1, 1, 1, 0), VertexDash.Solider));
                list.Add(new(new(result.X, 0), new(1, 1, 1, a), VertexDash.Solider));
                list.Add(new(new(result.X, 0 + target.Editor.ViewHeight), new(1, 1, 1, a), VertexDash.Solider));
                list.Add(new(new(result.X, 0 + target.Editor.ViewHeight), new(1, 1, 1, 0), VertexDash.Solider));
            }

            builder.PushViewMatrix(Matrix4x4.CreateTranslation(-target.Editor.ViewWidth / 2, -target.Editor.ViewHeight / 2, 0));
            builder.DrawSimpleLines(list, 1);
            builder.PopViewMatrix();
        }

        public void DrawXGridText(IFumenEditorDrawingContext target, IDrawCommandListBuilder builder, IEnumerable<CacheDrawXLineResult> drawLines)
        {
            if (target.Editor.IsPreviewMode)
                return;

            foreach (var pair in drawLines)
                builder.DrawString(
                    pair.XGridTotalUnitDisplay,
                    new(pair.X,
                    target.CurrentDrawingTargetContext.ViewRelativeRect.MaxY),
                    Vector2.One,
                    12,
                    0,
                    Vector4.One,
                    new(0, 0f),
                    FontStyle.Normal,
                    default
            );
        }
    }
}
