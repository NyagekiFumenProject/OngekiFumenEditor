using Caliburn.Micro;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Kernel.Graphics;
using OngekiFumenEditor.Utils;
using OngekiFumenEditor.Utils.ObjectPool;
using OpenTK.Graphics.OpenGL;
using Polyline2DCSharp;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using static OngekiFumenEditor.Kernel.Graphics.ILineDrawing;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Graphics.Drawing.TargetImpl
{
    public abstract class CommonLinesDrawTargetBase<T> : CommonBatchDrawTargetBase<T> where T : ConnectableStartObject
    {
        public virtual int LineWidth { get; } = 2;
        private ISimpleLineDrawing lineDrawing;
        TGrid shareTGrid = new TGrid();
        XGrid shareXGrid = new XGrid();
        private static VertexDash invailedDash = new VertexDash() { DashSize = 6, GapSize = 3 };

        public CommonLinesDrawTargetBase()
        {
            lineDrawing = IoC.Get<ISimpleLineDrawing>();
        }

        public abstract Vector4 GetLanePointColor(ConnectableObjectBase obj);

        public void FillLine(IFumenEditorDrawingContext target, T obj)
        {
            var color = GetLanePointColor(obj);
            var resT = obj.TGrid.ResT;
            var resX = obj.XGrid.ResX;

            [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.AggressiveInlining)]
            void PostPoint(TGrid tGrid, XGrid xGrid, bool isVailed)
            {
                var x = (float)XGridCalculator.ConvertXGridToX(xGrid, target.Editor);
                var y = (float)TGridCalculator.ConvertTGridToY_DesignMode(tGrid, target.Editor);

                lineDrawing.PostPoint(new(x, y), color, isVailed ? VertexDash.Solider : invailedDash);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            bool isVisible(TGrid tGrid) => target.TGridRange.VisiableMinTGrid <= tGrid || tGrid <= target.TGridRange.VisiableMaxTGrid;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            bool getNextIsVaild(ConnectableObjectBase o) => o.NextObject?.IsVaildPath ?? default;

            var prevVisible = isVisible(obj.TGrid);
            var alwaysDrawing = isVisible(obj.MinTGrid) && isVisible(obj.MaxTGrid);

            PostPoint(obj.TGrid, obj.XGrid, getNextIsVaild(obj));

            foreach (var childObj in obj.Children)
            {
                var visible = alwaysDrawing || isVisible(childObj.TGrid);
                var nextIsVaild = getNextIsVaild(childObj);

                if (visible || prevVisible)
                {
                    if (childObj.IsCurvePath)
                    {
                        foreach (var item in childObj.GetConnectionPaths())
                        {
                            shareTGrid.Unit = item.pos.Y / resT;
                            shareXGrid.Unit = item.pos.X / resX;
                            PostPoint(shareTGrid, shareXGrid, nextIsVaild);
                        }
                    }
                    else
                        PostPoint(childObj.TGrid, childObj.XGrid, nextIsVaild);
                }

                prevVisible = visible;
            }
        }

        public override void DrawBatch(IFumenEditorDrawingContext target, IEnumerable<T> starts)
        {
            foreach (var laneStart in starts)
            {
                lineDrawing.Begin(target, LineWidth);
                FillLine(target, laneStart);
                lineDrawing.End();
            }
        }
    }
}
