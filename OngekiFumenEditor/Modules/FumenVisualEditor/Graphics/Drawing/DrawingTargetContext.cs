using Microsoft.CodeAnalysis.Differencing;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.Collections;
using OngekiFumenEditor.Base.Collections.Base;
using OngekiFumenEditor.Kernel.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Graphics.Drawing
{
    public class DrawingTargetContext
    {
        public SoflanList CurrentSoflanList { get; set; }
        public SortableCollection<(TGrid minTGrid, TGrid maxTGrid), TGrid> VisibleTGridRanges { get; set; }
        public int SoflanGroupId { get; set; }
        public VisibleRect ViewRelativeRect { get; set; }
        public VisibleRect WorldRect { get; set; }
        public double ViewRelativeOriginY { get; set; }
        public Matrix4x4 ViewMatrix { get; set; }
        public Matrix4x4 ProjectionMatrix { get; set; }
        public float ViewWidth { get; set; }
        public float ViewHeight { get; set; }
        public float RenderScaleX { get; set; } = 1;
        public float RenderScaleY { get; set; } = 1;

        /// <summary>
        /// 为 true 时，<see cref="IDrawing.GetOverrideViewProjectMatrixOrDefault(DrawingTargetContext)"/> 会在视图投影矩阵之后追加一次
        /// Y 轴翻转（NDC y → -y）。离屏渲染用它把「GL 纹理行序（t=0 在底部）」对齐到 Bitmap/Skia 图像行序，避免结果贴回时上下颠倒。
        /// </summary>
        public bool FlipY { get; set; }
    }
}
