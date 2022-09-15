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
        private Dictionary<int, float> cacheCalc = new();

        public CommonLinesDrawTargetBase()
        {
            lineDrawing = IoC.Get<ILineDrawing>();
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
                cacheCalc[xGrid.TotalGrid] = x;

                var y = (float)TGridCalculator.ConvertTGridToY(tGrid, target.Fumen.BpmList, 1.0, 240);
                cacheCalc[tGrid.TotalGrid] = y;

                return new(new(x, y), color);
            }

            list.Add(calc(obj.TGrid, obj.XGrid));
            foreach (var child in obj.Children)
            {
                foreach (var item in child.GenPath())
                    list.Add(calc(new TGrid(item.pos.Y / resT), new XGrid(item.pos.X / resX)));
            }

            return (list.FirstOrDefault(), list.LastOrDefault());
        }

        public override void End()
        {
            cacheCalc.Clear();
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
