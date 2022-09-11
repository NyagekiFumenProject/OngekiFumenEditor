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
        private Dictionary<GridBase, float> cacheCalc = new();

        public CommonLinesDrawTargetBase()
        {
            lineDrawing = IoC.Get<ILineDrawing>();
        }

        public abstract Vector4 GetLanePointColor(ConnectableObjectBase obj);

        public IReadOnlyDictionary<GridBase, float> GetCurrentFrameCalculatedGridResult() => cacheCalc;

        public (LineVertex begin, LineVertex end) FillLine(IFumenPreviewer target, List<LineVertex> list, T obj)
        {
            var color = GetLanePointColor(obj);
            LineVertex calc(ConnectableObjectBase o)
            {
                var x = (float)XGridCalculator.ConvertXGridToX(o.XGrid, 30, target.ViewWidth, 1);
                cacheCalc[o.XGrid] = x;

                var y = (float)TGridCalculator.ConvertTGridToY(o.TGrid, target.Fumen.BpmList, 1.0, 240);
                cacheCalc[o.TGrid] = y;

                return new(new(x, y), color);
            }

            list.Add(calc(obj));
            foreach (var child in obj.Children)
                list.Add(calc(child));

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
