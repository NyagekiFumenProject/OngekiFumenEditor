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
using static OngekiFumenEditor.Modules.FumenPreviewer.Graphics.Drawing.ILineDrawing;

namespace OngekiFumenEditor.Modules.FumenPreviewer.Graphics.Drawing.Editors
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

        public void DrawLines(IFumenPreviewer target)
        {
            drawLines.Clear();

            var width = target.ViewWidth;
            var xUnitSpace = 4;
            var unitSize = (float)XGridCalculator.CalculateXUnitSize(30, width, xUnitSpace);
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
                list.Add(new(new(result.X, target.CurrentPlayTime), new(1, 1, 1, 0)));
                list.Add(new(new(result.X, target.CurrentPlayTime), new(1, 1, 1, 0.25f)));
                list.Add(new(new(result.X, target.CurrentPlayTime + target.ViewHeight), new(1, 1, 1, 0.25f)));
                list.Add(new(new(result.X, target.CurrentPlayTime + target.ViewHeight), new(1, 1, 1, 0)));
            }

            lineDrawing.Draw(target, list, 1);
        }

        public void DrawXGridText(IFumenPreviewer target)
        {
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
