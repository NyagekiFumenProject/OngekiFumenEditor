using Caliburn.Micro;
using OngekiFumenEditor.Core.Base;
using OngekiFumenEditor.Core.Base.OngekiObjects;
using OngekiFumenEditor.Core.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Kernel.Graphics;
using OngekiFumenEditor.Kernel.Graphics.DrawCommands;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;
using static OngekiFumenEditor.Kernel.Graphics.ILineDrawing;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Graphics.Drawing.TargetImpl
{
    public abstract class CommonLinesDrawTargetBase<T> : CommonBatchDrawTargetBase<T> where T : ConnectableStartObject
    {
        public virtual int LineWidth { get; } = 2;
        private static VertexDash invailedDash = new VertexDash(6, 3);

        public override void Initialize(IRenderManagerImpl impl)
        {
        }

        public abstract Vector4 GetLanePointColor(ConnectableObjectBase obj);

        public void FillLine(IFumenEditorDrawingContext target, IDrawCommandListBuilder builder, T start)
        {
            var color = GetLanePointColor(start);

            using var list = ObjectPool.GetPooledList<LineVertex>();
            VisibleLineVerticesQuery.QueryVisibleLineVertices(target, start, target.CurrentDrawingTargetContext.CurrentSoflanList, invailedDash, color, list);
            builder.DrawSimpleLines(list, LineWidth);
        }

        public override void DrawBatch(IFumenEditorDrawingContext target, IDrawCommandListBuilder builder, IEnumerable<T> starts)
        {
            foreach (var laneStart in starts)
            {
                FillLine(target, builder, laneStart);
            }
        }
    }
}
