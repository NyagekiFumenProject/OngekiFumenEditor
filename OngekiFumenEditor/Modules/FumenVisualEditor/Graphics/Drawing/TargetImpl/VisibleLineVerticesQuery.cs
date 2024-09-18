using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Utils;
using OngekiFumenEditor.Utils.ObjectPool;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using static OngekiFumenEditor.Base.Collections.SoflanList;
using static OngekiFumenEditor.Kernel.Graphics.ILineDrawing;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Graphics.Drawing.TargetImpl
{
    public static class VisibleLineVerticesQuery
    {
        public static void QueryVisibleLineVertices(IFumenEditorDrawingContext target, ConnectableStartObject start, VertexDash invailedDash, Vector4 color, IList<LineVertex> outVertices)
        {
            if (start is null)
                return;

            var resT = start.TGrid.ResT;
            var resX = start.XGrid.ResX;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            void PostPoint2(double tGridUnit, double xGridUnit, bool isVailed)
            {
                var x = (float)XGridCalculator.ConvertXGridToX(xGridUnit, target.Editor);
                var y = (float)target.ConvertToY(tGridUnit);

                outVertices.Add(new(new(x, y), color, isVailed ? VertexDash.Solider : invailedDash));
            }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            void PostPoint(TGrid tGrid, XGrid xGrid, bool isVailed) => PostPoint2(tGrid.TotalUnit, xGrid.TotalUnit, isVailed);
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            void PostObject(OngekiMovableObjectBase obj, bool isVailed) => PostPoint(obj.TGrid, obj.XGrid, isVailed);
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            bool getNextIsVaild(ConnectableObjectBase o) => o.NextObject?.IsVaildPath ?? true;

            var prevVisible = target.CheckVisible(start.TGrid);
            var alwaysDrawing = target.CheckRangeVisible(start.MinTGrid, start.MaxTGrid);

            PostObject(start, getNextIsVaild(start));
            var prevInvaild = true;
            var prevObj = start as ConnectableObjectBase;

            var soflanPositionList = target.Editor.IsDesignMode ?
                target.Editor.Fumen.Soflans.GetCachedSoflanPositionList_DesignMode(target.Editor.Fumen.BpmList) :
                target.Editor.Fumen.Soflans.GetCachedSoflanPositionList_PreviewMode(target.Editor.Fumen.BpmList);

            var minIdx = soflanPositionList.LastOrDefaultIndexByBinarySearch(start.MinTGrid, x => x.TGrid);
            var maxIdx = soflanPositionList.LastOrDefaultIndexByBinarySearch(start.MaxTGrid, x => x.TGrid);

            //enumerate all SoflanPoint which lane affected
            var affectedSoflanPoints = ObjectPool<List<SoflanPoint>>.Get();
            affectedSoflanPoints.Clear();

            //make reverse manually to optimze List::RemoveAt()
            for (int i = maxIdx; i >= minIdx + 1; i--)
                affectedSoflanPoints.Add(soflanPositionList[i]);

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            void CheckIfSoflanChanged(TGrid currentTGrid, bool isVailed)
                => CheckIfSoflanChanged2(currentTGrid.TotalUnit, isVailed);
            void CheckIfSoflanChanged2(double totalTGrid, bool isVailed)
            {
                /*
                 Check if there is any SoflanPoint before connectable object
                 If exist, just interpolate a new point to insert
                 */
                if (affectedSoflanPoints.Count == 0)
                    return;

                var checkTGrid = affectedSoflanPoints[^1].TGrid;
                var diff = checkTGrid.TotalUnit - totalTGrid;

                if (diff > 0)
                    return;

                if (diff < 0)
                {
                    var xGrid = start.CalulateXGrid(checkTGrid);
                    PostPoint(checkTGrid, xGrid, isVailed);
                }

                affectedSoflanPoints.RemoveAt(affectedSoflanPoints.Count - 1);
                //check again
                CheckIfSoflanChanged2(totalTGrid, isVailed);
            }

            foreach (var childObj in start.Children)
            {
                var visible = alwaysDrawing || target.CheckVisible(childObj.TGrid);
                var curIsVaild = childObj.IsVaildPath;
                if (prevInvaild != curIsVaild)
                {
                    CheckIfSoflanChanged(prevObj.TGrid, curIsVaild);
                    PostObject(prevObj, curIsVaild);
                    prevInvaild = curIsVaild;
                }

                if (prevVisible != visible && prevVisible == false)
                {
                    CheckIfSoflanChanged(prevObj.TGrid, prevInvaild);
                    PostObject(prevObj, prevInvaild);
                }

                if (visible || prevVisible)
                {
                    if (childObj.IsCurvePath)
                    {
                        foreach (var item in childObj.GetConnectionPaths())
                        {
                            var tGridUnit = item.pos.Y / resT;
                            CheckIfSoflanChanged2(tGridUnit, curIsVaild);
                            PostPoint2(tGridUnit, item.pos.X / resX, curIsVaild);
                        }
                    }
                    else
                    {
                        CheckIfSoflanChanged(childObj.TGrid, curIsVaild);
                        PostObject(childObj, curIsVaild);
                    }
                }

                prevObj = childObj;
                prevVisible = visible;
            }

            ObjectPool<List<SoflanPoint>>.Return(affectedSoflanPoints);
        }
    }
}
