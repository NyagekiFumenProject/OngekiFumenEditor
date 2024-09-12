using AngleSharp.Dom;
using Caliburn.Micro;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Kernel.Graphics;
using OngekiFumenEditor.Utils;
using OngekiFumenEditor.Utils.ObjectPool;
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
    public class HoldDrawingTarget : CommonDrawTargetBase<Hold>
    {
        public override IEnumerable<string> DrawTargetID { get; } = new string[] { "HLD", "CHD", "XHD" };

        public override int DefaultRenderOrder => 500;

        private ILineDrawing lineDrawing;

        private Vector4 colorHoldLeft;
        private Vector4 colorHoldCenter;
        private Vector4 colorHoldRight;
        private Vector4 colorHoldWallLeft;
        private Vector4 colorHoldWallRight;

        public HoldDrawingTarget() : base()
        {
            lineDrawing = IoC.Get<ILineDrawing>();

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
            var start = hold.ReferenceLaneStart;
            var holdEnd = hold.HoldEnd;
            var laneType = start?.LaneType;

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
                    var y = (float)target.ConvertToY(tGridUnit);

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

                using var d = ObjectPool<List<LineVertex>>.GetWithUsingDisposable(out var list, out _);
                list.Clear();
                VisibleLineVerticesQuery.QueryVisibleLineVertices(target, start, VertexDash.Solider, color, list);
                if (list.Count > 0)
                {
                    while (list.Count > 0 && holdPoint.Y > list[0].Point.Y)
                        list.RemoveAt(0);
                    if (list.Count >= 2)
                    {
                        var outSide = list[0];
                        var inSide = list[1];

                        if (checkDiscardByHorizon(inSide.Point, holdPoint, outSide.Point))
                            list.RemoveAt(0);
                    }
                    list.Insert(0, new LineVertex(holdPoint, color, VertexDash.Solider));
                    while (list.Count > 0 && holdEndPoint.Y < list[list.Count - 1].Point.Y)
                        list.RemoveAt(list.Count - 1);
                    if (list.Count >= 2)
                    {
                        var outSide = list[list.Count - 1];
                        var inSide = list[list.Count - 2];

                        if (checkDiscardByHorizon(inSide.Point, holdEndPoint, outSide.Point))
                            list.RemoveAt(list.Count - 1);
                    }
                    list.Add(new LineVertex(holdEndPoint, color, VertexDash.Solider));
                }
                else
                {
                    list.Add(new LineVertex(holdPoint, color, VertexDash.Solider));
                    list.Add(new LineVertex(holdEndPoint, color, VertexDash.Solider));
                }

                lineDrawing.Draw(target, list, 13);
            }
        }
    }
}