using Caliburn.Micro;
using NAudio.Gui;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Kernel.Graphics;
using OngekiFumenEditor.Kernel.Graphics.Text;
using OngekiFumenEditor.Kernel.Graphics.DrawCommands;
using OngekiFumenEditor.Kernel.Graphics.DrawCommands.DefaultDrawCommands;
using OngekiFumenEditor.Modules.FumenSoflanGroupListViewer.Models;
using OngekiFumenEditor.Utils;
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
    internal sealed class IndividualSoflanAreaDrawingTarget : CommonBatchDrawTargetBase<IndividualSoflanArea>
    {
        private IImage texture;

        public override IEnumerable<string> DrawTargetID { get; } = ["ISF"];

        public override int DefaultRenderOrder { get; } = 0;

        public override DrawingVisible DefaultVisible => DrawingVisible.Design;

        public override void Initialize(IRenderManagerImpl impl)
        {
            texture = ResourceUtils.OpenReadTextureFromFile(impl, @".\Resources\editor\tri.png");
        }

        private static Dictionary<int, Vector4> cacheColor = new();

        public static Vector4 CalculateColorBySoflanGroup(int soflanGroup)
        {
            if (cacheColor.TryGetValue(soflanGroup, out var color))
                return color;

            return cacheColor[soflanGroup] = SoflanGroupColor.Get(soflanGroup);
        }

        public override void DrawBatch(IFumenEditorDrawingContext target, IDrawCommandListBuilder builder, IEnumerable<IndividualSoflanArea> isfList)
        {
            using var lineVertex = ObjectPool.GetPooledList<LineVertex>();
            using var texList = ObjectPool.GetPooledList<(Vector2 size, Vector2 position, float rotation, Vector4 color)>();
            using var hightTexList = ObjectPool.GetPooledList<(Vector2 size, Vector2 position, float rotation, Vector4 color)>();

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
                var swapT = isf.EndIndicator.TGrid.TotalUnit < isf.TGrid.TotalUnit;
                var minTGrid = swapT ? isf.EndIndicator.TGrid : isf.TGrid;
                var maxTGrid = swapT ? isf.TGrid : isf.EndIndicator.TGrid;

                var leftX = (float)XGridCalculator.ConvertXGridToX(minXGrid, target.Editor);
                var topY = (float)target.ConvertToViewRelativeY_DefaultSoflanGroup(maxTGrid);

                var rightX = (float)XGridCalculator.ConvertXGridToX(maxXGrid, target.Editor);
                var bottomY = (float)target.ConvertToViewRelativeY_DefaultSoflanGroup(minTGrid);

                lineVertex.Add(new LineVertex(lineVertex.Count > 0 ? lineVertex[lineVertex.Count - 1].Point : default, transparent, dash));
                lineVertex.Add(new LineVertex(new(leftX, topY), transparent, dash));

                //画一个方框
                lineVertex.Add(new LineVertex(new(leftX, topY), color, dash));
                lineVertex.Add(new LineVertex(new(rightX, topY), color, dash));
                lineVertex.Add(new LineVertex(new(rightX, bottomY), color, dash));
                lineVertex.Add(new LineVertex(new(leftX, bottomY), color, dash));
                lineVertex.Add(new LineVertex(new(leftX, topY), color, dash));

                var startX = (float)XGridCalculator.ConvertXGridToX(isf.XGrid, target.Editor);
                var startY = (float)target.ConvertToViewRelativeY_DefaultSoflanGroup(isf.TGrid);
                var endX = (float)XGridCalculator.ConvertXGridToX(isf.EndIndicator.XGrid, target.Editor);
                var endY = (float)target.ConvertToViewRelativeY_DefaultSoflanGroup(isf.EndIndicator.TGrid);

                // 角标跟随各自对象坐标：拖动时被拖的三角始终贴在指针下，越过另一顶点后也不会错位。
                // 矩形框本身仍按 min/max 的四条边绘制。
                var startTriPos = new Vector2(startX + texSize / 2 + bias, startY + texSize / 2 + bias);
                var endTriPos = new Vector2(endX - texSize / 2 - bias, endY - texSize / 2 - bias);

                texList.Add((new(texSize, texSize), startTriPos, 0, Vector4.One));
                texList.Add((new(texSize, texSize), endTriPos, MathF.PI, Vector4.One));

                target.RegisterSelectableObject(isf, startTriPos, new(texSize, texSize));
                target.RegisterSelectableObject(isf.EndIndicator, endTriPos, new(texSize, texSize));

                builder.DrawString($"SFL:{isf.SoflanGroup}", new Vector2(rightX, topY) + new Vector2(1, 11), Vector2.One, 16, 0, color, new(0, 0.5f), FontStyle.Normal, default);

                if (isf.IsSelected || isf.EndIndicator.IsSelected)
                {
                    var centerX = (leftX + rightX) / 2;
                    var centerY = (topY + bottomY) / 2;

                    var rectColor = color;
                    rectColor.W = 0.35f;

                    builder.DrawPolygon(Primitive.TriangleStrip, new[]
                    {
                        new PolygonVertex(new(leftX, bottomY), rectColor),
                        new PolygonVertex(new(centerX, centerY), rectColor),
                        new PolygonVertex(new(leftX, topY), rectColor),
                        new PolygonVertex(new(centerX, centerY), rectColor),
                        new PolygonVertex(new(rightX, topY), rectColor),
                        new PolygonVertex(new(centerX, centerY), rectColor),
                        new PolygonVertex(new(rightX, bottomY), rectColor),
                        new PolygonVertex(new(centerX, centerY), rectColor),
                        new PolygonVertex(new(leftX, bottomY), rectColor),
                    });

                    hightTexList.Add((new(texSize * highlightScale, texSize * highlightScale),
                        startTriPos + new Vector2(highlightScale, highlightScale), 0, Vector4.One));
                    hightTexList.Add((new(texSize * highlightScale, texSize * highlightScale),
                        endTriPos - new Vector2(highlightScale, highlightScale), MathF.PI, Vector4.One));

                    //画一个方框
                    lineVertex.Add(new LineVertex(new(leftX, topY), highlightLineColor, VertexDash.Solider));
                    lineVertex.Add(new LineVertex(new(rightX, topY), highlightLineColor, VertexDash.Solider));
                    lineVertex.Add(new LineVertex(new(rightX, bottomY), highlightLineColor, VertexDash.Solider));
                    lineVertex.Add(new LineVertex(new(leftX, bottomY), highlightLineColor, VertexDash.Solider));
                    lineVertex.Add(new LineVertex(new(leftX, topY), highlightLineColor, VertexDash.Solider));
                }
            }

            builder.DrawSimpleLines(lineVertex, 1.5f);
            builder.DrawHighlightBatchTexture(texture, hightTexList);
            builder.DrawTexture(texture, texList);

        }
    }
}
