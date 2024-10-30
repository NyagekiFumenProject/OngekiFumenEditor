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
        public static void QueryVisibleLineVertices(IFumenEditorDrawingContext target, ConnectableStartObject start, VertexDash invailedDash, Vector4 color, List<LineVertex> outVertices)
        {
            if (start is null)
                return;

            var resT = start.TGrid.ResT;
            var resX = start.XGrid.ResX;

            var tempVertices = ObjectPool<List<LineVertex>>.Get();
            tempVertices.Clear();

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            void PostPoint2(double tGridUnit, double xGridUnit, bool isVailed)
            {
                var x = (float)XGridCalculator.ConvertXGridToX(xGridUnit, target.Editor);
                var y = (float)target.ConvertToY(tGridUnit);
                var vert = new LineVertex(new(x, y), color, isVailed ? VertexDash.Solider : invailedDash);

                tempVertices.Add(vert);
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
                while (affectedSoflanPoints.Count > 0)
                {
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
                }
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

            //optimze vertices
            var idx = 0;
            for (; idx < tempVertices.Count - 3; idx++)
            {
                var a1 = tempVertices[idx];
                var a2 = tempVertices[idx + 1];
                var b1 = tempVertices[idx + 2];
                var b2 = tempVertices[idx + 3];

                if (!(a1 == b1 && a2 == b2))
                    outVertices.Add(a1);

                if ((a1.Point.X == a2.Point.X && a2.Point.X == b1.Point.X) || (a1.Point.Y == a2.Point.Y && a2.Point.Y == b1.Point.Y))
                {
                    outVertices.Add(b1);
                    idx += 2;
                }
                else if (a1.Point == a2.Point)
                {
                    idx += 1;
                }
            }

            //add remain vertices
            outVertices.AddRange(tempVertices.Skip(idx));

            ObjectPool<List<SoflanPoint>>.Return(affectedSoflanPoints);
            ObjectPool<List<LineVertex>>.Return(tempVertices);
        }

        //BACKUP
        public static void _QueryVisibleLineVertices(IFumenEditorDrawingContext target, ConnectableStartObject start, VertexDash invailedDash, Vector4 color, IList<LineVertex> outVertices)
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

            var affectedSoflanPoints = ObjectPool<List<SoflanPoint>>.Get();

            var soflanPositionList = target.Editor.IsDesignMode ?
                target.Editor.Fumen.Soflans.GetCachedSoflanPositionList_DesignMode(target.Editor.Fumen.BpmList) :
                target.Editor.Fumen.Soflans.GetCachedSoflanPositionList_PreviewMode(target.Editor.Fumen.BpmList);

            var cur = start.Children.FirstOrDefault();
            while (cur != null)
            {
                var prev = cur.PrevObject;

                var minTGrid = prev.TGrid;
                var maxTGrid = cur.TGrid;

                if (minTGrid > maxTGrid)
                    (minTGrid, maxTGrid) = (maxTGrid, minTGrid);

                if (target.CheckRangeVisible(minTGrid, maxTGrid))
                {
                    var minIdx = soflanPositionList.LastOrDefaultIndexByBinarySearch(minTGrid, x => x.TGrid);
                    var maxIdx = soflanPositionList.LastOrDefaultIndexByBinarySearch(maxTGrid, x => x.TGrid);

                    affectedSoflanPoints.Clear();
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

                    //visible, draw then
                    var isVaild = cur.IsVaildPath;

                    PostObject(prev, isVaild);

                    if (cur.IsCurvePath)
                    {
                        foreach (var item in cur.GetConnectionPaths())
                        {
                            var tGridUnit = item.pos.Y / resT;
                            CheckIfSoflanChanged2(tGridUnit, isVaild);
                            PostPoint2(tGridUnit, item.pos.X / resX, isVaild);
                        }
                    }
                    else
                    {
                        CheckIfSoflanChanged(cur.TGrid, isVaild);
                        PostObject(cur, isVaild);
                    }
                }

                cur = cur.NextObject;
            }

            ObjectPool<List<SoflanPoint>>.Return(affectedSoflanPoints);
        }
    }
}
