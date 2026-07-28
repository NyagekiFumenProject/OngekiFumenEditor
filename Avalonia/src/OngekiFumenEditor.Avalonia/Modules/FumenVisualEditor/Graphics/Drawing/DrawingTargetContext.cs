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
        public VisibleRect Rect { get; set; }
        public Matrix4 ViewMatrix { get; set; }
        public Matrix4 ProjectionMatrix { get; set; }
        public float ViewWidth { get; set; }
        public float ViewHeight { get; set; }
    }
}


