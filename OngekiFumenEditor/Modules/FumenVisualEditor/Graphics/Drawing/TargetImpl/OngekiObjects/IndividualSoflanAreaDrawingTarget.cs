using Caliburn.Micro;
using NAudio.Gui;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Kernel.Graphics;
using OngekiFumenEditor.Kernel.Graphics.Base;
using OngekiFumenEditor.Utils;
using OngekiFumenEditor.Utils.ObjectPool;
using OpenTK.Graphics.OpenGL;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Numerics;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using Xv2CoreLib;
using static OngekiFumenEditor.Kernel.Graphics.ILineDrawing;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Graphics.Drawing.TargetImpl.OngekiObjects
{
    [Export(typeof(IFumenEditorDrawingTarget))]
    internal class IndividualSoflanAreaDrawingTarget : CommonBatchDrawTargetBase<IndividualSoflanArea>
    {
        private readonly IStringDrawing stringDrawing;
        private readonly ILineDrawing lineDrawing;
        private readonly ITextureDrawing textureDrawing;
        private readonly IPolygonDrawing polygonDrawing;
        private readonly IHighlightBatchTextureDrawing highlightDrawing;
        private readonly Texture texture;
        private static readonly int colorSeed = RandomHepler.Random(int.MinValue, int.MaxValue);

        public override IEnumerable<string> DrawTargetID { get; } = ["ISF"];

        public override int DefaultRenderOrder { get; } = 0;

        public override DrawingVisible DefaultVisible => DrawingVisible.Design;

        public IndividualSoflanAreaDrawingTarget()
        {
            stringDrawing = IoC.Get<IStringDrawing>();
            lineDrawing = IoC.Get<ISimpleLineDrawing>();
            textureDrawing = IoC.Get<ITextureDrawing>();
            polygonDrawing = IoC.Get<IPolygonDrawing>();
            highlightDrawing = IoC.Get<IHighlightBatchTextureDrawing>();

            texture = ResourceUtils.OpenReadTextureFromFile(@".\Resources\editor\tri.png");
        }

        private static Dictionary<int, Vector4> cacheColor = new();

        public static Vector4 CalculateColorBySoflanGroup(int soflanGroup)
        {
            if (cacheColor.TryGetValue(soflanGroup, out var color))
                return color;

            (float r, float g, float b) HsvToRgb(float h, float s, float v)
            {
                h = Math.Clamp(h, 0, 360);
                s = Math.Clamp(s, 0, 1);
                v = Math.Clamp(v, 0, 1);

                float c = v * s;
                float x = c * (1 - Math.Abs((h / 60) % 2 - 1));
                float m = v - c;

                (float r, float g, float b) rgb;

                if (h < 60) rgb = (c, x, 0);
                else if (h < 120) rgb = (x, c, 0);
                else if (h < 180) rgb = (0, c, x);
                else if (h < 240) rgb = (0, x, c);
                else if (h < 300) rgb = (x, 0, c);
                else rgb = (c, 0, x);

                return (rgb.r + m, rgb.g + m, rgb.b + m);
            }

            float hue = Math.Abs((HashCode.Combine(soflanGroup, soflanGroup) ^ colorSeed) % 360);
            float saturation = 1f;
            float value = 1f;

            var (r, g, b) = HsvToRgb(hue, saturation, value);

            color = new Vector4(r, g, b, 1);
            return cacheColor[soflanGroup] = color;
        }

        public override void DrawBatch(IFumenEditorDrawingContext target, IEnumerable<IndividualSoflanArea> isfList)
        {
            var lineVertex = ObjectPool<List<LineVertex>>.Get();
            lineVertex.Clear();
            var texList = ObjectPool<List<(Vector2 size, Vector2 position, float rotation)>>.Get();
            texList.Clear();
            var hightTexList = ObjectPool<List<(Vector2 size, Vector2 position, float rotation)>>.Get();
            hightTexList.Clear();

            var dash = new VertexDash(8, 2);
            var texSize = 14;
            var bias = -1;
            var transparent = new Vector4(0, 0, 0, 0);
            var highlightScale = 1.5f;
            var highlightLineColor = new Vector4(252.0f / 255.0f, 1.0f, 75.0f / 255.0f, 0.5f);

            foreach (var isf in isfList)
            {
                var color = CalculateColorBySoflanGroup(isf.SoflanGroup);

                var minXGrid = Math.Min(isf.XGrid.TotalUnit, isf.EndIndicator.XGrid.TotalUnit);
                var maxXGrid = Math.Max(isf.XGrid.TotalUnit, isf.EndIndicator.XGrid.TotalUnit);
                var minTGrid = isf.TGrid;
                var maxTGrid = isf.EndIndicator.TGrid;

                var leftX = (float)XGridCalculator.ConvertXGridToX(minXGrid, target.Editor);
                var topY = (float)target.ConvertToY_DefaultSoflanGroup(maxTGrid);

                var rightX = (float)XGridCalculator.ConvertXGridToX(maxXGrid, target.Editor);
                var bottomY = (float)target.ConvertToY_DefaultSoflanGroup(minTGrid);

                lineVertex.Add(new LineVertex(lineVertex.LastOrDefault()?.Point ?? default, transparent, dash));
                lineVertex.Add(new LineVertex(new(leftX, topY), transparent, dash));

                //画一个方框
                lineVertex.Add(new LineVertex(new(leftX, topY), color, dash));
                lineVertex.Add(new LineVertex(new(rightX, topY), color, dash));
                lineVertex.Add(new LineVertex(new(rightX, bottomY), color, dash));
                lineVertex.Add(new LineVertex(new(leftX, bottomY), color, dash));
                lineVertex.Add(new LineVertex(new(leftX, topY), color, dash));

                var topRightTriPos = new Vector2(rightX - texSize / 2 - bias, topY - texSize / 2 - bias);
                var bottomLeftTriPos = new Vector2(leftX + texSize / 2 + bias, bottomY + texSize / 2 + bias);

                texList.Add((new(texSize, texSize), bottomLeftTriPos, 0));
                texList.Add((new(texSize, texSize), topRightTriPos, MathF.PI));

                target.RegisterSelectableObject(isf, bottomLeftTriPos, new(texSize, texSize));
                target.RegisterSelectableObject(isf.EndIndicator, topRightTriPos, new(texSize, texSize));

                stringDrawing.Draw($"SFL:{isf.SoflanGroup}", new Vector2(rightX, topY) + new Vector2(1, 11), Vector2.One, 16, 0, color, new(0, 0.5f), IStringDrawing.StringStyle.Normal, target, default, out _);

                if (isf.IsSelected || isf.EndIndicator.IsSelected)
                {
                    var centerX = (leftX + rightX) / 2;
                    var centerY = (topY + bottomY) / 2;

                    var rectColor = color;
                    rectColor.W = 0.35f;

                    polygonDrawing.Begin(target, PrimitiveType.TriangleStrip);
                    {
                        polygonDrawing.PostPoint(new(leftX, bottomY), rectColor);
                        polygonDrawing.PostPoint(new(centerX, centerY), rectColor);
                        polygonDrawing.PostPoint(new(leftX, topY), rectColor);
                        polygonDrawing.PostPoint(new(centerX, centerY), rectColor);
                        polygonDrawing.PostPoint(new(rightX, topY), rectColor);
                        polygonDrawing.PostPoint(new(centerX, centerY), rectColor);
                        polygonDrawing.PostPoint(new(rightX, bottomY), rectColor);
                        polygonDrawing.PostPoint(new(centerX, centerY), rectColor);
                        polygonDrawing.PostPoint(new(leftX, bottomY), rectColor);
                    }
                    polygonDrawing.End();

                    hightTexList.Add((new(texSize * highlightScale, texSize * highlightScale),
                        bottomLeftTriPos + new Vector2(highlightScale, highlightScale), 0));
                    hightTexList.Add((new(texSize * highlightScale, texSize * highlightScale),
                        topRightTriPos - new Vector2(highlightScale, highlightScale), MathF.PI));

                    //画一个方框
                    lineVertex.Add(new LineVertex(new(leftX, topY), highlightLineColor, VertexDash.Solider));
                    lineVertex.Add(new LineVertex(new(rightX, topY), highlightLineColor, VertexDash.Solider));
                    lineVertex.Add(new LineVertex(new(rightX, bottomY), highlightLineColor, VertexDash.Solider));
                    lineVertex.Add(new LineVertex(new(leftX, bottomY), highlightLineColor, VertexDash.Solider));
                    lineVertex.Add(new LineVertex(new(leftX, topY), highlightLineColor, VertexDash.Solider));
                }
            }

            lineDrawing.Draw(target, lineVertex, 1.5f);
            highlightDrawing.Draw(target, texture, hightTexList);
            textureDrawing.Draw(target, texture, texList);

            ObjectPool.Return(lineVertex);
            ObjectPool.Return(texList);
            ObjectPool.Return(hightTexList);
        }
    }
}
