using OngekiFumenEditor.Avalonia.Base;
using OngekiFumenEditor.Avalonia.Base.Collections;
using OngekiFumenEditor.Avalonia.Base.Collections.Base;
using OngekiFumenEditor.Avalonia.Kernel.Graphics;
using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Graphics.Drawing
{
    public class DrawingTargetContext
    {
        public SoflanList CurrentSoflanList { get; set; }
        public SortableCollection<(TGrid minTGrid, TGrid maxTGrid), TGrid> VisibleTGridRanges { get; set; }
        public int SoflanGroupId { get; set; }
        public VisibleRect ViewRelativeRect { get; set; }
        public VisibleRect WorldRect { get; set; }
        public double ViewRelativeOriginY { get; set; }
        public Matrix4 ViewMatrix { get; set; }
        public Matrix4 ProjectionMatrix { get; set; }
        public float ViewWidth { get; set; }
        public float ViewHeight { get; set; }

        /// <summary>
        /// 本帧统一的时间快照，由 OnEditorRender 在帧首读取一次。
        /// 渲染期不得再读取 editor 的实时播放时间，否则会出现同帧内时间不一致。
        /// </summary>
        public TimeSpan CurrentTime { get; set; }

        /// <summary>
        /// 本帧统一的当前 TGrid 快照（不套用 EditorOffsetMs，语义等同 editor.GetCurrentTGrid()）。
        /// </summary>
        public TGrid CurrentTGrid { get; set; }
    }
}
