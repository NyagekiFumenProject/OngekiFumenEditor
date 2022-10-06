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
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using static OngekiFumenEditor.Modules.FumenVisualEditor.Graphics.Drawing.ILineDrawing;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Graphics.Drawing.TargetImpl
{
    public abstract class CommonLinesDrawTargetBase<T> : CommonBatchDrawTargetBase<T> where T : ConnectableStartObject
    {
        public virtual int LineWidth { get; } = 2;
        private ISimpleLineDrawing lineDrawing;
        TGrid shareTGrid = new TGrid();
        XGrid shareXGrid = new XGrid();

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
            void PostPoint(TGrid tGrid, XGrid xGrid)
            {
                var x = (float)XGridCalculator.ConvertXGridToX(xGrid, target.Editor);
                var y = (float)TGridCalculator.ConvertTGridToY(tGrid, target.Editor);

                lineDrawing.PostPoint(new(x, y), color, VertexDash.Solider);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            bool isVisible(TGrid tGrid)
            {
                return target.Rect.VisiableMinTGrid <= tGrid || tGrid <= target.Rect.VisiableMaxTGrid;
            }

            var prevVisible = isVisible(obj.TGrid);
            var alwaysDrawing = isVisible(obj.MinTGrid) && isVisible(obj.MaxTGrid);

            PostPoint(obj.TGrid, obj.XGrid);

            foreach (var childObj in obj.Children)
            {
                var visible = alwaysDrawing || isVisible(childObj.TGrid);

                if (visible || prevVisible)
                {
                    if (childObj.IsCurvePath)
                    {
                        foreach (var item in childObj.GetConnectionPaths())
                        {
                            shareTGrid.Unit = item.pos.Y / resT;
                            shareXGrid.Unit = item.pos.X / resX;
                            PostPoint(shareTGrid, shareXGrid);
                        }
                    }
                    else
                        PostPoint(childObj.TGrid, childObj.XGrid);
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
