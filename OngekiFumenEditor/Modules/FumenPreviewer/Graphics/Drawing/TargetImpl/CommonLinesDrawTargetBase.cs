using Caliburn.Micro;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Modules.FumenVisualEditor;
using OngekiFumenEditor.Utils;
using OngekiFumenEditor.Utils.ObjectPool;
using OpenTK.Graphics.OpenGL;
using Polyline2DCSharp;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using static OngekiFumenEditor.Modules.FumenPreviewer.Graphics.Drawing.ILineDrawing;

namespace OngekiFumenEditor.Modules.FumenPreviewer.Graphics.Drawing.TargetImpl
{
    public abstract class CommonLinesDrawTargetBase<T> : CommonBatchDrawTargetBase<T> where T : ConnectableStartObject
    {
        public virtual int LineWidth { get; } = 2;
        private ILineDrawing lineDrawing;
        TGrid shareTGrid = new TGrid();
        XGrid shareXGrid = new XGrid();

        public CommonLinesDrawTargetBase()
        {
            lineDrawing = IoC.Get<ISimpleLineDrawing>();
        }

        public abstract Vector4 GetLanePointColor(ConnectableObjectBase obj);

        public (LineVertex begin, LineVertex end) FillLine(IFumenPreviewer target, List<LineVertex> list, T obj)
        {
            var color = GetLanePointColor(obj);
            var resT = obj.TGrid.ResT;
            var resX = obj.XGrid.ResX;

            LineVertex calc(TGrid tGrid, XGrid xGrid)
            {
                var x = (float)XGridCalculator.ConvertXGridToX(xGrid, 30, target.ViewWidth, 1);
                var y = (float)TGridCalculator.ConvertTGridToY(tGrid, target.Fumen.BpmList, 1.0, 240);

                return new(new(x, y), color);
            }

            list.Add(calc(obj.TGrid, obj.XGrid));
            foreach (var child in obj.Children)
            {
                if (child.PathControls.Any())
                {
                    foreach (var item in child.GenPath())
                    {
                        shareTGrid.Unit = item.pos.Y / resT;
                        shareXGrid.Unit = item.pos.X / resX;
                        list.Add(calc(shareTGrid, shareXGrid));
                    }
                }
                else
                {
                    list.Add(calc(child.TGrid, child.XGrid));
                }
            }

            return (list.FirstOrDefault(), list.LastOrDefault());
        }

        public override void End()
        {
            base.End();
        }

        public override void DrawBatch(IFumenPreviewer target, IEnumerable<T> starts)
        {
            using var d = ObjectPool<List<LineVertex>>.GetWithUsingDisposable(out var list, out _);

            foreach (var start in starts)
            {
                list.Clear();
                FillLine(target, list, start);
                lineDrawing.Draw(target, list, LineWidth);
            }
        }
    }
}
