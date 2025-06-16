using Caliburn.Micro;
using NAudio.Gui;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.EditorObjects;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Kernel.Graphics;
using OngekiFumenEditor.Modules.FumenSoflanGroupListViewer;
using OngekiFumenEditor.Utils;
using OngekiFumenEditor.Utils.ObjectPool;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Numerics;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Media3D;
using static OngekiFumenEditor.Kernel.Graphics.ILineDrawing;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Graphics.Drawing.TargetImpl.OngekiObjects.Soflans
{
    [Export(typeof(IFumenEditorDrawingTarget))]
    public class DurationSoflanDrawingTarget : CommonBatchDrawTargetBase<OngekiObjectBase>
    {
        private readonly IStringDrawing stringDrawing;
        private readonly ILineDrawing lineDrawing;
        private readonly IPolygonDrawing polygonDrawing;
        private readonly SoflanPlaceholdQuery placeholdQuery;

        public override DrawingVisible DefaultVisible => DrawingVisible.Design;

        public override IEnumerable<string> DrawTargetID { get; } = [
            "SFL","[SFL_End]",
            "[INTP_SFL]","[INTP_SFL_End]",
            "[KEY_SFL]"
        ];

        public override int DefaultRenderOrder { get; } = 1500;

        public DurationSoflanDrawingTarget()
        {
            stringDrawing = IoC.Get<IRenderManager>().StringDrawing;
            lineDrawing = IoC.Get<IRenderManager>().SimpleLineDrawing;
            polygonDrawing = IoC.Get<IRenderManager>().PolygonDrawing;
            placeholdQuery = new SoflanPlaceholdQuery();
        }

        public override void DrawBatch(IFumenEditorDrawingContext target, IEnumerable<OngekiObjectBase> objs)
        {
            var soflans = objs.Select(x => x switch
            {
                ISoflan s => s,
                Soflan.SoflanEndIndicator e => e.RefSoflan,
                _ => null
            }).Distinct().OrderBy(x => x.SoflanGroup);

            placeholdQuery.UpdatePositionIndexesForNewFrame(soflans.Select(x => x.GetHashCode()));

            float margin = 20f;
            float width = 70f;
            var endDash = new VertexDash(7, 5);

            using var _d = ObjectPool<List<LineVertex>>.GetWithUsingDisposable(out var lines, out _);
            lines.Clear();
            using var _d3 = ObjectPool<List<(Vector2, Vector4)>>.GetWithUsingDisposable(out var polygonPoints, out _);
            polygonPoints.Clear();
            using var _d4 = ObjectPool<List<LineVertex>>.GetWithUsingDisposable(out var lines2, out _);
            lines2.Clear();
            using var _d2 = ObjectPool<List<(string, Vector2, Vector4, OngekiTimelineObjectBase)>>.GetWithUsingDisposable(out var strings, out _);
            strings.Clear();

            void PushLine(LineVertex start, LineVertex end)
            {
                lines.Add(start with
                {
                    Color = Vector4.Zero
                });
                lines.Add(start);
                lines.Add(end);
                lines.Add(end with
                {
                    Color = Vector4.Zero
                });
            }
            void PushLine2(LineVertex start, LineVertex end)
            {
                lines2.Add(start with
                {
                    Color = Vector4.Zero
                });
                lines2.Add(start);
                lines2.Add(end);
                lines2.Add(end with
                {
                    Color = Vector4.Zero
                });
            }

            void PushString(string content, Vector2 pos, Vector4 color, OngekiTimelineObjectBase obj)
            {
                strings.Add((content, pos, color, obj));
            }

            void DrawCommonSoflanStart(Soflan soflan, int queryPlaceholdPosIndex)
            {
                var color = GetSoflanGroupColor(soflan.SoflanGroup);
                var placeholdCenterX = target.CurrentDrawingTargetContext.Rect.ButtomRight.X - ((queryPlaceholdPosIndex * width + width / 2) + margin);
                var placeholdY = (float)target.ConvertToY_DefaultSoflanGroup(soflan.TGrid);

                var placeholdLeftX = placeholdCenterX - width / 2;
                var placeholdRightX = placeholdCenterX + width / 2;
                PushLine(new(new(placeholdLeftX, placeholdY), color, VertexDash.Solider), new(new(placeholdRightX, placeholdY), color, VertexDash.Solider));

                var placeholdTextY = placeholdY - 10;
                PushString($"[{soflan.SoflanGroup}]{soflan.Speed:F2}x", new(placeholdCenterX, placeholdTextY), color, soflan as OngekiTimelineObjectBase);
                var placeholdText2Y = placeholdY + 15;
            }

            void DrawSoflan(Soflan soflan, int queryPlaceholdPosIndex)
            {
                DrawCommonSoflanStart(soflan, queryPlaceholdPosIndex);

                var color = GetSoflanGroupColor(soflan.SoflanGroup);
                var placeholdCenterX = target.CurrentDrawingTargetContext.Rect.ButtomRight.X - ((queryPlaceholdPosIndex * width + width / 2) + margin);
                var startCenterY = (float)target.ConvertToY_DefaultSoflanGroup(soflan.TGrid);
                var endCenterY = (float)target.ConvertToY_DefaultSoflanGroup(soflan.EndTGrid);

                var placeholdLeftX = placeholdCenterX - width / 2 * 0.75f;
                var placeholdRightX = placeholdCenterX + width / 2 * 0.75f;
                PushLine2(new(new(placeholdLeftX, endCenterY), color, endDash), new(new(placeholdRightX, endCenterY), color, endDash));

                var size = new Vector2(width + 10, 12);
                var pos = new Vector2(placeholdCenterX, endCenterY + 0.5f);
                target.RegisterSelectableObject(soflan.EndIndicator, pos, size);

                var placeholdTextY = endCenterY + 15;
                var endIndicator = soflan.EndIndicator as InterpolatableSoflan.InterpolatableSoflanIndicator;
                PushLine(new(new(placeholdCenterX, startCenterY), color, VertexDash.Solider), new(new(placeholdCenterX, endCenterY), color, VertexDash.Solider));

                if (soflan.EndIndicator.IsSelected)
                {
                    var borderPos = new Vector2(pos.X, pos.Y);
                    var bx = borderPos.X;
                    var by = borderPos.Y;
                    var hw = size.X / 2;
                    var hh = size.Y / 2;

                    lines.Add(new(new(bx - hw, by + hh), new(1, 1, 0, 0), VertexDash.Solider));
                    lines.Add(new(new(bx - hw, by + hh), new(1, 1, 0, 1), VertexDash.Solider));
                    lines.Add(new(new(bx + hw, by + hh), new(1, 1, 0, 1), VertexDash.Solider));
                    lines.Add(new(new(bx + hw, by - hh), new(1, 1, 0, 1), VertexDash.Solider));
                    lines.Add(new(new(bx - hw, by - hh), new(1, 1, 0, 1), VertexDash.Solider));
                    lines.Add(new(new(bx - hw, by + hh), new(1, 1, 0, 1), VertexDash.Solider));
                    lines.Add(new(new(bx - hw, by + hh), new(1, 1, 0, 0), VertexDash.Solider));
                }
            }

            void DrawInterpolatableSoflan(InterpolatableSoflan soflan, int queryPlaceholdPosIndex)
            {
                DrawCommonSoflanStart(soflan, queryPlaceholdPosIndex);

                var color = GetSoflanGroupColor(soflan.SoflanGroup);
                var placeholdCenterX = target.CurrentDrawingTargetContext.Rect.ButtomRight.X - ((queryPlaceholdPosIndex * width + width / 2) + margin);
                var startCenterY = (float)target.ConvertToY_DefaultSoflanGroup(soflan.TGrid);
                var endCenterY = (float)target.ConvertToY_DefaultSoflanGroup(soflan.EndTGrid);

                var placeholdLeftX = placeholdCenterX - width / 2 * 0.75f;
                var placeholdRightX = placeholdCenterX + width / 2 * 0.75f;
                PushLine(new(new(placeholdLeftX, endCenterY), color, endDash), new(new(placeholdRightX, endCenterY), color, endDash));

                var placeholdTextY = endCenterY + 15;
                var endIndicator = soflan.EndIndicator as InterpolatableSoflan.InterpolatableSoflanIndicator;
                PushString($"[{soflan.SoflanGroup}]{endIndicator.Speed:F2}x", new(placeholdCenterX, placeholdTextY), color, endIndicator);

                PushLine(new(new(placeholdCenterX, startCenterY), color, VertexDash.Solider), new(new(placeholdCenterX, endCenterY), color, VertexDash.Solider));
            }

            void DrawKeyframeSoflan(KeyframeSoflan keyframeSoflan, int queryPlaceholdPosIndex)
            {
                var color = GetSoflanGroupColor(keyframeSoflan.SoflanGroup);
                var placeholdCenterX = target.CurrentDrawingTargetContext.Rect.ButtomRight.X - ((queryPlaceholdPosIndex * width + width / 2) + margin);
                var placeholdY = (float)target.ConvertToY_DefaultSoflanGroup(keyframeSoflan.TGrid);

                var placeholdLeftX = placeholdCenterX - width / 2;
                var placeholdRightX = placeholdCenterX + width / 2;
                PushLine(new(new(placeholdLeftX, placeholdY), color, VertexDash.Solider), new(new(placeholdRightX, placeholdY), color, VertexDash.Solider));

                var placeholdTextY = placeholdY - 10;
                PushString($"[{keyframeSoflan.SoflanGroup}]{keyframeSoflan.Speed:F2}x", new(placeholdCenterX, placeholdTextY), color, keyframeSoflan);
                var placeholdText2Y = placeholdY + 15;

                polygonPoints.Add((new(placeholdCenterX + 5, placeholdY), color));
                polygonPoints.Add((new(placeholdCenterX - 5, placeholdY), color));
                polygonPoints.Add((new(placeholdCenterX, placeholdY + 10), color));
            }

            using var _d5 =
                target.Editor.Fumen.IndividualSoflanAreaMap.Keys
                .Select(x => target.Editor.Fumen.IndividualSoflanAreaMap.TryGetOrCreateSoflanGroupWrapItem(x, out _))
                .Where(x => x.IsDisplaySoflanDesignMode)
                .Select(x => x.SoflanGroupId)
                .ToHashSetWithObjectPool(out var visibleSoflanGroups);

            //var queryPlaceholdPosIndex = 0;
            foreach (var s in soflans.Where(x => visibleSoflanGroups.Contains(x.SoflanGroup)))
            {
                var queryPlaceholdPosIndex = placeholdQuery.QueryPositionIndex(s.GetHashCode());
                switch (s)
                {
                    case InterpolatableSoflan interpolatableSoflan:
                        DrawInterpolatableSoflan(interpolatableSoflan, queryPlaceholdPosIndex);
                        break;
                    case Soflan soflan:
                        DrawSoflan(soflan, queryPlaceholdPosIndex);
                        break;
                    case KeyframeSoflan keyframeSoflan:
                        DrawKeyframeSoflan(keyframeSoflan, queryPlaceholdPosIndex);
                        break;
                    default:
                        break;
                }

                //queryPlaceholdPosIndex = (queryPlaceholdPosIndex + 1) % 2;
            }

            foreach (var (str, pos, color, obj) in strings)
            {
                stringDrawing.Draw(
                    str,
                    pos,
                    Vector2.One,
                    15,
                    0,
                    color,
                    new Vector2(0.5f, 0.5f),
                    IStringDrawing.StringStyle.Bold,
                    target,
                    default, out var size);
                target.RegisterSelectableObject(obj, pos, size ?? Vector2.Zero);

                if (obj.IsSelected)
                {
                    var borderPos = new Vector2(pos.X, pos.Y - 2.8f);
                    var bx = borderPos.X;
                    var by = borderPos.Y;
                    var hw = size.Value.X / 2 + 4;
                    var hh = size.Value.Y / 2 + 1.2f;

                    lines.Add(new(new(bx - hw, by + hh), new(1, 1, 0, 0), VertexDash.Solider));
                    lines.Add(new(new(bx - hw, by + hh), new(1, 1, 0, 1), VertexDash.Solider));
                    lines.Add(new(new(bx + hw, by + hh), new(1, 1, 0, 1), VertexDash.Solider));
                    lines.Add(new(new(bx + hw, by - hh), new(1, 1, 0, 1), VertexDash.Solider));
                    lines.Add(new(new(bx - hw, by - hh), new(1, 1, 0, 1), VertexDash.Solider));
                    lines.Add(new(new(bx - hw, by + hh), new(1, 1, 0, 1), VertexDash.Solider));
                    lines.Add(new(new(bx - hw, by + hh), new(1, 1, 0, 0), VertexDash.Solider));
                }
            }

            lineDrawing.Draw(target, lines, 2.5f);
            lineDrawing.Draw(target, lines2, 4f);

            polygonDrawing.Begin(target, Primitive.Triangles);
            {
                foreach (var (pos, color) in polygonPoints)
                    polygonDrawing.PostPoint(pos, color);
            }
            polygonDrawing.End();
        }

        private Vector4 GetSoflanGroupColor(int soflanGroup)
        {
            return IndividualSoflanAreaDrawingTarget.CalculateColorBySoflanGroup(soflanGroup);
        }
    }
}
