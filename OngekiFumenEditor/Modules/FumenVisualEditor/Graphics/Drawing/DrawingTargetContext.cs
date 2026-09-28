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
        /// 本帧的播放时间快照：<c>OnEditorRender</c> 帧首只读一次，帧内所有时间消费者都走它。
        /// 渲染线程与 UI 线程的时间推进因此不会撕裂到同一帧里。
        /// </summary>
        public TimeSpan CurrentTime { get; set; }

        /// <summary>本帧的当前 TGrid 快照（由 <see cref="CurrentTime"/> 换算）。</summary>
        public TGrid CurrentTGrid { get; set; }

        /// <summary>
        /// 本帧的视口 TGrid 快照。预览模式下视口由滚动位置决定，尾段余量允许它领先于被钳制的
        /// 播放时间（<see cref="CurrentTGrid"/>）；设计模式下两者相同。
        /// </summary>
        public TGrid ViewportTGrid { get; set; }

        /// <summary>
        /// When true, <see cref="IDrawing.GetOverrideViewProjectMatrixOrDefault(DrawingTargetContext)"/> appends a single
        /// Y-axis flip (NDC y → -y) after the view-projection matrix. Offscreen rendering uses it to align the GL texture
        /// row order (t = 0 at the bottom) with the Bitmap/Skia image row order, so that pasted-back results are not upside down.
        /// </summary>
        public bool FlipY { get; set; }
    }
}
