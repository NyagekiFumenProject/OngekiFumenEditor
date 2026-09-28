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
        /// When true, <see cref="IDrawing.GetOverrideViewProjectMatrixOrDefault(DrawingTargetContext)"/> appends a single
        /// Y-axis flip (NDC y → -y) after the view-projection matrix. Offscreen rendering uses it to align the GL texture
        /// row order (t = 0 at the bottom) with the Bitmap/Skia image row order, so that pasted-back results are not upside down.
        /// </summary>
        public bool FlipY { get; set; }
    }
}
