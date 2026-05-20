using AngleSharp.Dom;
using Caliburn.Micro;
using OngekiFumenEditor.Core.Base;
using OngekiFumenEditor.Core.Base.OngekiObjects;
using OngekiFumenEditor.Kernel.Graphics;
using OngekiFumenEditor.Utils;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using static OngekiFumenEditor.Kernel.Graphics.ILineDrawing;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Graphics.Drawing.TargetImpl.OngekiObjects.Holds
{
    [Export(typeof(IFumenEditorDrawingTarget))]
    public sealed class HoldDrawingTarget : CommonDrawTargetBase<Hold>
    {
        public override IEnumerable<string> DrawTargetID { get; } = new string[] { "HLD", "CHD", "XHD" };

        public override int DefaultRenderOrder => 500;

        private ILineDrawing lineDrawing;

        private Vector4 colorHoldLeft;
        private Vector4 colorHoldCenter;
        private Vector4 colorHoldRight;
        private Vector4 colorHoldWallLeft;
        private Vector4 colorHoldWallRight;

        public override void Initialize(IRenderManagerImpl impl)
        {
            lineDrawing = impl.LineDrawing;

            Properties.EditorGlobalSetting.Default.PropertyChanged += EditorGlobalSettingPropertyChanged;
            RebuildColors();
        }

        private void EditorGlobalSettingPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (!e.PropertyName.StartsWith("ColorHold"))
                return;

            RebuildColors();
        }

        private void RebuildColors()
        {
            static void build(ref Vector4 v, System.Drawing.Color c)
            {
                v = c.ToVector4();
                v.W = 0.75f;
            }

            build(ref colorHoldLeft, Properties.EditorGlobalSetting.Default.ColorHoldLeft);
            build(ref colorHoldCenter, Properties.EditorGlobalSetting.Default.ColorHoldCenter);
            build(ref colorHoldRight, Properties.EditorGlobalSetting.Default.ColorHoldRight);

            build(ref colorHoldWallLeft, Properties.EditorGlobalSetting.Default.ColorHoldWallLeft);
            build(ref colorHoldWallRight, Properties.EditorGlobalSetting.Default.ColorHoldWallRight);

            //Log.LogInfo($"hold color has been rebuild.");
        }

        public override void Draw(IFumenEditorDrawingContext target, Hold hold)
        {
            var holdEnd = hold.HoldEnd;
            if (holdEnd is null)
                return;

            var start = hold.ReferenceLaneStart;
            var laneType = start?.LaneType;
            var soflanList = target.Editor._cacheSoflanGroupRecorder.GetCache(hold);

            var color = laneType switch
            {
                LaneType.Left => colorHoldLeft,
                LaneType.Center => colorHoldCenter,
                LaneType.Right => colorHoldRight,
                LaneType.WallLeft => colorHoldWallLeft,
                LaneType.WallRight => colorHoldWallRight,
                _ => new Vector4(1, 1, 1, 0.75f),
            };

            if (holdEnd != null)
            {
                Vector2 PostPoint2(double tGridUnit, double xGridUnit)
                {
                    var x = (float)XGridCalculator.ConvertXGridToX(xGridUnit, target.Editor);
                    var y = (float)target.ConvertToY(tGridUnit, soflanList);

                    return new(x, y);
                }

                var holdPoint = PostPoint2(hold.TGrid.TotalUnit, hold.XGrid.TotalUnit);
                var holdEndPoint = PostPoint2(holdEnd.TGrid.TotalUnit, holdEnd.XGrid.TotalUnit);

                bool checkDiscardByHorizon(Vector2 prev, Vector2 end, Vector2 cur)
                {
                    //判断三个点是否都在一个水平上
                    if (prev.Y == cur.Y && end.Y == cur.Y)
                    {
                        /*
								   good                discard
						o-----------x---------o----------x----------------
						|           |         |          |
						prevX     curX_1   endPosX     curX_2
						 */
                        var checkX = cur.X;
                        if (checkX < MathF.Min(prev.X, end.X) || checkX > MathF.Max(prev.X, end.X))
                            return true;
                    }
                    return false;
                }

                using var list = ObjectPool.GetPooledList<LineVertex>();
                VisibleLineVerticesQuery.QueryVisibleLineVertices(target, start, soflanList, VertexDash.Solider, color, list);
                if (list.Count > 0)
                {
                    var startIdx = 0;
                    while (startIdx < list.Count && holdPoint.Y > list[startIdx].Point.Y)
                        startIdx++;

                    if (list.Count - startIdx >= 2)
                    {
                        var outSide = list[startIdx];
                        var inSide = list[startIdx + 1];

                        if (checkDiscardByHorizon(inSide.Point, holdPoint, outSide.Point))
                            startIdx++;
                    }

                    var endIdx = list.Count - 1;
                    while (endIdx >= startIdx && holdEndPoint.Y < list[endIdx].Point.Y)
                        endIdx--;

                    if (endIdx >= startIdx)
                    {
                        var outSide = list[endIdx];
                        var inSidePoint = endIdx > startIdx ? list[endIdx - 1].Point : holdPoint;

                        if (checkDiscardByHorizon(inSidePoint, holdEndPoint, outSide.Point))
                            endIdx--;
                    }

                    using var clippedList = ObjectPool.GetPooledList<LineVertex>();
                    clippedList.Add(new LineVertex(holdPoint, color, VertexDash.Solider));
                    for (var i = startIdx; i <= endIdx; i++)
                        clippedList.Add(list[i]);
                    clippedList.Add(new LineVertex(holdEndPoint, color, VertexDash.Solider));

                    lineDrawing.Draw(target, clippedList, 13);
                }
                else
                {
                    list.Add(new LineVertex(holdPoint, color, VertexDash.Solider));
                    list.Add(new LineVertex(holdEndPoint, color, VertexDash.Solider));
                    lineDrawing.Draw(target, list, 13);
                }
            }
        }
    }
}