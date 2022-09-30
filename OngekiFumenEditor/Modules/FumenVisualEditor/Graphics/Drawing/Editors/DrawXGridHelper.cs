using Caliburn.Micro;
using FontStashSharp;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Modules.FumenVisualEditor;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using OngekiFumenEditor.Utils.ObjectPool;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Documents;
using static OngekiFumenEditor.Modules.FumenVisualEditor.Graphics.Drawing.ILineDrawing;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Graphics.Drawing.Editors
{
    public class DrawXGridHelper
    {
        public struct CacheDrawLineResult
        {
            public float X { get; set; }
            public string XGridTotalUnitDisplay { get; set; }
        }

        private List<CacheDrawLineResult> drawLines = new();

        private IStringDrawing stringDrawing;
        private ILineDrawing lineDrawing;

        public DrawXGridHelper()
        {
            stringDrawing = IoC.Get<IStringDrawing>();
            lineDrawing = IoC.Get<ISimpleLineDrawing>();
        }

        public void DrawLines(IFumenEditorDrawingContext target)
        {
            if (target.Editor.EditorObjectVisibility != System.Windows.Visibility.Visible)
                return;

            drawLines.Clear();

            var width = target.ViewWidth;
            var xUnitSpace = (float)target.Editor.Setting.XGridUnitSpace;
            var maxDisplayXUnit = target.Editor.Setting.XGridDisplayMaxUnit;

            var unitSize = (float)XGridCalculator.CalculateXUnitSize(maxDisplayXUnit, width, xUnitSpace);
            var totalUnitValue = 0f;

            for (float totalLength = width / 2 + unitSize; totalLength < width; totalLength += unitSize)
            {
                totalUnitValue += xUnitSpace;

                drawLines.Add(new()
                {
                    X = totalLength,
                    XGridTotalUnitDisplay = totalUnitValue.ToString()
                });

                drawLines.Add(new()
                {
                    X = (width / 2) - (totalLength - (width / 2)),
                    XGridTotalUnitDisplay = (-totalUnitValue).ToString()
                });
            }
            drawLines.Add(new()
            {
                X = width / 2
            });

            using var d = ObjectPool<List<LineVertex>>.GetWithUsingDisposable(out var list, out _);
            list.Clear();

            foreach (var result in drawLines)
            {
                list.Add(new(new(result.X, target.Rect.Height), new(1, 1, 1, 0)));
                list.Add(new(new(result.X, 0), new(1, 1, 1, 0.25f)));
                list.Add(new(new(result.X, 0 + target.Rect.Height), new(1, 1, 1, 0.25f)));
                list.Add(new(new(result.X, 0 + target.Rect.Height), new(1, 1, 1, 0)));
            }

            lineDrawing.PushOverrideViewProjectMatrix(OpenTK.Mathematics.Matrix4.CreateTranslation(-target.ViewWidth / 2, -target.ViewHeight / 2, 0) * target.ProjectionMatrix);
            lineDrawing.Draw(target, list, 1);
            lineDrawing.PopOverrideViewProjectMatrix(out _);
        }

        public void DrawXGridText(IFumenEditorDrawingContext target)
        {
            if (target.Editor.EditorObjectVisibility != System.Windows.Visibility.Visible)
                return;

            foreach (var pair in drawLines)
                stringDrawing.Draw(
                    pair.XGridTotalUnitDisplay,
                    new(pair.X,
                    target.CurrentPlayTime + target.ViewHeight),
                    Vector2.One,
                    12,
                    0,
                    Vector4.One,
                    new(0, 0f),
                    IStringDrawing.StringStyle.Normal,
                    target,
                    default,
                    out _
            );
        }
    }
}
