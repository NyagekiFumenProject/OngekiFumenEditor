using Caliburn.Micro;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Base.OngekiObjects.Lane.Base;
using OngekiFumenEditor.Modules.FumenVisualEditor;
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
using static OngekiFumenEditor.Modules.FumenVisualEditor.Graphics.Drawing.TargetImpl.CommonLinesDrawTargetBase<OngekiFumenEditor.Base.OngekiObjects.Lane.Base.LaneStartBase>;
using static OngekiFumenEditor.Modules.FumenVisualEditor.Graphics.Drawing.ILineDrawing;
using AngleSharp.Dom;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Graphics.Drawing.TargetImpl.OngekiObjects
{
    [Export(typeof(IDrawingTarget))]
    public class HoldDrawingTarget : CommonDrawTargetBase<Hold>
    {
        public override IEnumerable<string> DrawTargetID { get; } = new string[] { "HLD", "CHD", "XHD" };

        public override int DefaultRenderOrder => 500;

        private TapDrawingTarget tapDraw;
        private ILineDrawing lineDrawing;

        public HoldDrawingTarget() : base()
        {
            tapDraw = IoC.Get<TapDrawingTarget>();
            lineDrawing = IoC.Get<ILineDrawing>();
        }

        public override void Draw(IFumenEditorDrawingContext target, Hold hold)
        {
            var start = hold.ReferenceLaneStart;
            var holdEnd = hold.HoldEnd;
            var laneType = start?.LaneType;

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
                var y = (float)TGridCalculator.ConvertTGridToY(obj.TGrid, target.Editor);
                var x = (float)XGridCalculator.ConvertXGridToX(obj.XGrid, target.Editor);
                list.Add(new(new(x, y), color, VertexDash.Solider));
            }

            if (holdEnd != null)
            {
                Upsert(hold);
                if (start != null)
                {
                    var itor = start.Children.AsEnumerable<ConnectableObjectBase>().Prepend(start).Where(x => hold.TGrid <= x.TGrid && x.TGrid <= holdEnd.TGrid).GetEnumerator();
                    itor.MoveNext();
                    var cur = itor.Current;
                    var prev = null as ConnectableObjectBase;

                    while (itor.MoveNext())
                    {
                        Upsert(cur);
                        prev = cur;
                        cur = itor.Current;
                    }

                    if (cur?.TGrid != prev?.TGrid)
                        Upsert(cur);
                }
                Upsert(holdEnd);
                lineDrawing.Draw(target, list, 13);
            }

            //draw taps
            tapDraw.Begin(target);
            tapDraw.Draw(target, laneType, hold, hold.IsCritical);
            if (holdEnd != null)
                tapDraw.Draw(target, laneType, holdEnd, false);
            tapDraw.End();
        }
    }
}
