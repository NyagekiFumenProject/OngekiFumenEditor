using Caliburn.Micro;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Base.OngekiObjects.Lane.Base;
using OngekiFumenEditor.Utils;
using OngekiFumenEditor.Utils.ObjectPool;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Drawing;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using static OngekiFumenEditor.Kernel.Graphics.ILineDrawing;
using AngleSharp.Dom;
using OngekiFumenEditor.Kernel.Graphics;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Graphics.Drawing.TargetImpl.OngekiObjects.Holds
{
    [Export(typeof(IFumenEditorDrawingTarget))]
    public class HoldDrawingTarget : CommonDrawTargetBase<Hold>
    {
        public override IEnumerable<string> DrawTargetID { get; } = new string[] { "HLD", "CHD", "XHD" };

        public override int DefaultRenderOrder => 500;

        private ILineDrawing lineDrawing;

        public HoldDrawingTarget() : base()
        {
            lineDrawing = IoC.Get<ILineDrawing>();
        }

        public override void Draw(IFumenEditorDrawingContext target, Hold hold)
        {
            var start = hold.ReferenceLaneStart;
            var holdEnd = hold.HoldEnd;
            var laneType = start?.LaneType;

            var shareTGrid = new TGrid();
            var shareXGrid = new XGrid();

            var color = laneType switch
            {
                LaneType.Left => new Vector4(1, 0, 0, 0.75f),
                LaneType.Center => new Vector4(0, 1, 0, 0.75f),
                LaneType.Right => new Vector4(0, 0, 1, 0.75f),
                LaneType.WallLeft => new Vector4(35 / 255.0f, 4 / 255.0f, 117 / 255.0f, 0.75f),
                LaneType.WallRight => new Vector4(136 / 255.0f, 3 / 255.0f, 152 / 255.0f, 0.75f),
                _ => new Vector4(1, 1, 1, 0.75f),
            };

            //draw line
            using var d = ObjectPool<List<LineVertex>>.GetWithUsingDisposable(out var list, out _);
            list.Clear();

            void Upsert<T>(T obj) where T : IHorizonPositionObject, ITimelineObject
            {
                var y = (float)target.ConvertToY(obj.TGrid);
                var x = (float)XGridCalculator.ConvertXGridToX(obj.XGrid, target.Editor);
                list.Add(new(new(x, y), color, VertexDash.Solider));
            }

            void Upsert2((float, float) pos)
            {
                var y = (float)target.ConvertToY(pos.Item1);
                var x = (float)XGridCalculator.ConvertXGridToX(pos.Item2, target.Editor);
                list.Add(new(new(x, y), color, VertexDash.Solider));
            }

            if (holdEnd != null)
            {
                var resT = hold.TGrid.ResT;
                var resX = hold.XGrid.ResX;

                Upsert(hold);
                if (start != null)
                {
                    var itor = start.Children.SelectMany(x => x.GetConnectionPaths()).Select(x =>
                    {
                        return (x.pos.Y / resT, x.pos.X / resX);
                    }).Prepend(((float)start.TGrid.TotalUnit, (float)start.XGrid.TotalUnit))
                    .Where(pos => hold.TGrid.TotalUnit <= pos.Item1 && pos.Item1 <= holdEnd.TGrid.TotalUnit).GetEnumerator();
                    var hasValue = itor.MoveNext();
                    var cur = itor.Current;
                    var prev = (-2857f, 0f);

                    while (itor.MoveNext())
                    {
                        Upsert2(cur);
                        prev = cur;
                        cur = itor.Current;
                    }

                    if (cur.Item1 != prev.Item1 && hasValue)
                        Upsert2(cur);
                }

                Upsert(holdEnd);
                lineDrawing.Draw(target, list, 13);
            }
        }
    }
}