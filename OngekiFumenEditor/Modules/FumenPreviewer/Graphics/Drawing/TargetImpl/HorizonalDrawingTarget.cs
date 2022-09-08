using FontStashSharp;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Base.OngekiObjects.Lane.Base;
using OngekiFumenEditor.Modules.FumenPreviewer.Graphics.Drawing.Base;
using OngekiFumenEditor.Modules.FumenPreviewer.Graphics.Drawing.Shaders;
using OngekiFumenEditor.Modules.FumenPreviewer.Graphics.Drawing.TargetImpl.Lane;
using OngekiFumenEditor.Modules.FumenVisualEditor;
using OngekiFumenEditor.Utils;
using OngekiFumenEditor.Utils.ObjectPool;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using Polyline2DCSharp;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Drawing;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Vector2 = System.Numerics.Vector2;
using Vector4 = OpenTK.Mathematics.Vector4;

namespace OngekiFumenEditor.Modules.FumenPreviewer.Graphics.Drawing.TargetImpl
{
    [Export(typeof(IDrawingTarget))]
    public class HorizonalDrawingTarget : CommonDrawTargetBase<OngekiTimelineObjectBase>, IDisposable
    {
        public record RegisterDrawingInfo(OngekiTimelineObjectBase TimelineObject, double Y);

        private readonly Shader shader;
        private readonly int vbo;
        private readonly int vao;
        private HashSet<RegisterDrawingInfo> registeredObjects = new();
        private DrawStringHelper stringHelper;
        private OngekiFumen fumen;

        public const int LINE_DRAW_MAX = 100;
        public int LineWidth { get; set; } = 2;

        public HorizonalDrawingTarget()
        {
            stringHelper = new DrawStringHelper();
            shader = CommonLineShader.Shared;

            vbo = GL.GenBuffer();
            vao = GL.GenVertexArray();

            Init();
        }

        public static StateStack DefaultRenderStateStack { get; } = CommonLinesDrawTargetBase<OngekiObjectBase>.DefaultRenderStateStack;

        private void Init()
        {
            GL.BindVertexArray(vao);
            {
                GL.BindBuffer(BufferTarget.ArrayBuffer, vbo);
                {
                    GL.BufferData(BufferTarget.ArrayBuffer, new IntPtr(sizeof(float) * LINE_DRAW_MAX * 6),
                        IntPtr.Zero, BufferUsageHint.DynamicCopy);

                    GL.EnableVertexAttribArray(0);
                    GL.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, sizeof(float) * 6, 0);

                    GL.EnableVertexAttribArray(1);
                    GL.VertexAttribPointer(1, 4, VertexAttribPointerType.Float, false, sizeof(float) * 6, sizeof(float) * 2);
                }
                GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
            }
            GL.BindVertexArray(0);
        }

        public override void BeginDraw()
        {
            registeredObjects.Clear();
            base.BeginDraw();
        }

        public override void EndDraw()
        {
            DrawInternal(registeredObjects);

            base.EndDraw();
        }

        public void Draw(List<LinePoint> list, int lineWidth, bool drawDash = false)
        {
            if (list.Count < 2)
                return;

            GL.LineWidth(lineWidth);
            shader.Begin();
            shader.PassUniform("Model", Matrix4.CreateTranslation(-Previewer.ViewWidth / 2, -Previewer.ViewHeight / 2, 0));
            shader.PassUniform("ViewProjection", Previewer.ViewProjectionMatrix);
            GL.BindVertexArray(vao);

            var arrBuffer = ArrayPool<float>.Shared.Rent(LINE_DRAW_MAX * 6);
            var arrBufferIdx = 0;

            void Copy(LinePoint lp)
            {
                arrBuffer[(6 * arrBufferIdx) + 0] = lp.Point.X;
                arrBuffer[(6 * arrBufferIdx) + 1] = lp.Point.Y;
                arrBuffer[(6 * arrBufferIdx) + 2] = lp.Color.X;
                arrBuffer[(6 * arrBufferIdx) + 3] = lp.Color.Y;
                arrBuffer[(6 * arrBufferIdx) + 4] = lp.Color.Z;
                arrBuffer[(6 * arrBufferIdx) + 5] = lp.Color.W;
                arrBufferIdx++;
            }

            void FlushDraw()
            {
                //GL.InvalidateBufferData(vbo);
                GL.BufferSubData(BufferTarget.ArrayBuffer, IntPtr.Zero, arrBufferIdx * sizeof(float) * 6, arrBuffer);
                DefaultRenderStateStack.PushState();
                {
                    GL.Enable(EnableCap.LineSmooth);
                    GL.Hint(HintTarget.LineSmoothHint, HintMode.Nicest);
                    GL.DrawArrays(PrimitiveType.LineStrip, 0, arrBufferIdx);
                }
                DefaultRenderStateStack.PopState();
                arrBufferIdx = 0;
            }

            GL.BindBuffer(BufferTarget.ArrayBuffer, vbo);
            {
                unsafe
                {

                    var prevLinePoint = list[0];

                    foreach (var item in list.SequenceWrap(LINE_DRAW_MAX - 1))
                    {
                        Copy(prevLinePoint);
                        foreach (var q in item)
                        {
                            Copy(q);
                            prevLinePoint = q;
                        }

                        FlushDraw();
                    }
                }
            }

            ArrayPool<float>.Shared.Return(arrBuffer);
            GL.BindVertexArray(0);
            shader.End();
        }

        public virtual void Dispose()
        {
            GL.DeleteVertexArray(vao);
            GL.DeleteBuffer(vbo);
        }

        public override IEnumerable<string> DrawTargetID { get; } = new[]
        {
            "MET","SFL","BPM","EST","CLK","LBK","[LBK_End]","[SFL_End]"
        };

        private static Dictionary<string, FSColor> colors = new()
        {
            {"MET", FSColor.LightGreen },
            {"SFL", FSColor.LightCyan },
            {"BPM", FSColor.Pink },
            {"EST", FSColor.Yellow },
            {"CLK", FSColor.CadetBlue },
            {"LBK", FSColor.HotPink },
            {"[LBK_End]", FSColor.HotPink },
            {"[SFL_End]", FSColor.LightCyan },
        };

        public override void Draw(OngekiTimelineObjectBase ongekiObject, OngekiFumen fumen)
        {
            registeredObjects.Add(new(ongekiObject, TGridCalculator.ConvertTGridToY(ongekiObject.TGrid, fumen.BpmList, 1.0, 240)));
            this.fumen = fumen;
        }

        private void DrawInternal(IEnumerable<RegisterDrawingInfo> objects)
        {
            foreach (var g in objects.GroupBy(x => x.TimelineObject.TGrid.TotalGrid))
            {
                var y = (float)g.FirstOrDefault().Y;
                using var d3 = g.ToListWithObjectPool(out var actualItems);
                if ((y < Previewer.CurrentPlayTime) || y > (Previewer.CurrentPlayTime + Previewer.ViewHeight))
                {
                    actualItems.RemoveAll(x => x.TimelineObject switch
                    {
                        LaneBlockArea or LaneBlockArea.LaneBlockAreaEndIndicator or Soflan or Soflan.SoflanEndIndicator => true,
                        _ => false
                    });
                    if (actualItems.Count == 0)
                        continue;
                }

                using var d = actualItems.Select(x => colors[x.TimelineObject.IDShortName]).OrderBy(x => x.PackedValue).ToListWithObjectPool(out var regColors);
                var per = 1.0f * Previewer.ViewWidth / regColors.Count;
                using var d2 = ObjectPool<List<LinePoint>>.GetWithUsingDisposable(out var segList, out _);
                segList.Clear();
                for (int i = 0; i < regColors.Count; i++)
                {
                    var c = regColors[i];
                    segList.Add(new LinePoint(new(per * i, y), new(c.R / 255.0f, c.G / 255.0f, c.B / 255.0f, c.A / 255.0f)));
                    segList.Add(new LinePoint(new(per * (i + 1), y), new(c.R / 255.0f, c.G / 255.0f, c.B / 255.0f, c.A / 255.0f)));
                }

                Draw(segList, LineWidth);

                //draw range line if need
                foreach (var obj in actualItems)
                {
                    switch (obj.TimelineObject)
                    {
                        case LaneBlockArea.LaneBlockAreaEndIndicator laneBlockEnd:
                            DrawLaneBlockArea(laneBlockEnd.RefLaneBlockArea);
                            break;
                        case LaneBlockArea laneBlock:
                            DrawLaneBlockArea(laneBlock);
                            break;
                        default:
                            break;
                    }
                }

                DrawDescText(y, actualItems);
            }
        }

        private void DrawLaneBlockArea(LaneBlockArea lbk)
        {
            using var d2 = ObjectPool<List<LinePoint>>.GetWithUsingDisposable(out var segList, out _);
            segList.Clear();

            var offsetX = (lbk.Direction == LaneBlockArea.BlockDirection.Left ? -1 : 1) * 10;
            var color = lbk.Direction == LaneBlockArea.BlockDirection.Left ? WallLaneDrawTarget.LeftWallColor : WallLaneDrawTarget.RightWallColor;
            var lastP = new OpenTK.Mathematics.Vector2(int.MinValue, int.MinValue);

            #region Generate LBK lines

            void PostPointByXTGrid(XGrid xGrid, TGrid tGrid, bool isStroke = true)
            {
                if (xGrid is null)
                    return;
                var x = (float)XGridCalculator.ConvertXGridToX(xGrid, 30, Previewer.ViewWidth, 1) + offsetX;
                var y = (float)TGridCalculator.ConvertTGridToY(tGrid, fumen.BpmList, 1.0, 240);

                var p = new OpenTK.Mathematics.Vector2(x, y);
                if (lastP != p)
                {
                    segList.Add(new(p, color));
                    lastP = p;
                }
            }

            void PostPointByTGrid(ConnectableChildObjectBase obj, TGrid grid, bool isStroke = true)
            {
                var xGrid = obj.CalulateXGrid(grid);
                PostPointByXTGrid(xGrid, grid, isStroke);
            }

            void ProcessConnectable(ConnectableChildObjectBase obj, TGrid minTGrid, TGrid maxTGrid)
            {
                var minTotalGrid = minTGrid.TotalGrid;
                var maxTotalGrid = maxTGrid.TotalGrid;

                if (!obj.IsCurvePath)
                {
                    //直线，优化
                    PostPointByTGrid(obj, minTGrid);
                    PostPointByTGrid(obj, maxTGrid);
                }
                else
                {
                    //PostPointByXTGrid(obj.CalulateXGrid(minTGrid), minTGrid);
                    using var d = ObjectPool<List<Vector2>>.GetWithUsingDisposable(out var list, out _);
                    list.Clear();

                    foreach ((var gridVec2, var isVaild) in obj.GenPath().Where(x => x.pos.Y <= maxTotalGrid && x.pos.Y >= minTotalGrid))
                    {
                        if (!isVaild)
                        {
                            PostPointByXTGrid(obj.PrevObject.XGrid, minTGrid, false);
                            PostPointByXTGrid(obj.XGrid, maxTGrid, false);
                            return;
                        }
                        list.Add(new(gridVec2.X, gridVec2.Y));
                    }
                    foreach (var gridVec2 in list)
                        PostPointByXTGrid(new(gridVec2.X / obj.XGrid.ResX), new(gridVec2.Y / obj.TGrid.ResT));
                }
            }

            void ProcessWallLane(LaneStartBase wallStartLane, TGrid minTGrid, TGrid maxTGrid)
            {
                foreach (var child in wallStartLane.Children)
                {
                    if (child.TGrid < minTGrid)
                        continue;
                    if (child.PrevObject.TGrid > maxTGrid)
                        break;

                    var childMinTGrid = MathUtils.Max(minTGrid, child.PrevObject.TGrid);
                    var childMaxTGrid = MathUtils.Min(maxTGrid, child.TGrid);

                    ProcessConnectable(child, childMinTGrid, childMaxTGrid);
                }
            }

            #endregion

            using var d = lbk.GetAffactableWallLanes(fumen).ToListWithObjectPool(out var list);
            var beginTGrid = lbk.TGrid;
            var endTGrid = lbk.EndIndicator.TGrid;
            var isNext = false;
            foreach (var start in list)
            {
                if (isNext)
                    PostPointByXTGrid(start.XGrid, start.TGrid, false);
                ProcessWallLane(start, beginTGrid, endTGrid);
                isNext = true;
            }

            //postprocess segments
            if (segList.Count >= 2)
            {
                //start
                var a = segList[0];
                var b = segList[1];

                var y = a.Point.Y + Math.Min((b.Point.Y - a.Point.Y) * 0.1f, 20);
                var x = (float)MathUtils.CalculateXFromTwoPointFormFormula(y, a.Point.X, a.Point.Y, b.Point.X, b.Point.Y);

                var c = new LinePoint(new(x, y), a.Color);
                segList.Insert(1, c);
                segList[0] = a with { Color = Vector4.Zero };

                //end
                a = segList[segList.Count - 2];
                b = segList[segList.Count - 1];

                y = b.Point.Y - Math.Min((b.Point.Y - a.Point.Y) * 0.1f, 20);
                x = (float)MathUtils.CalculateXFromTwoPointFormFormula(y, a.Point.X, a.Point.Y, b.Point.X, b.Point.Y);

                c = new LinePoint(new(x, y), a.Color);
                segList.Insert(segList.Count - 1, c);
                segList[segList.Count - 1] = b with { Color = Vector4.Zero };
            }

            //todo 换个表达形式
            Draw(segList, 10, true);
        }

        private void DrawDescText(float y, IEnumerable<RegisterDrawingInfo> group)
        {
            string formatObj(OngekiObjectBase s) => s switch
            {
                BPMChange o => $"BPM:{(int)o.BPM}",
                MeterChange o => $"MET:{o.Bunbo}/{o.BunShi}",
                Soflan o => $"SPD:{(int)o.Speed:F2}x",
                Soflan.SoflanEndIndicator o => $"{formatObj(o.RefSoflan)}_End",
                LaneBlockArea o => $"LBK:{o.Direction}",
                LaneBlockArea.LaneBlockAreaEndIndicator o => $"{formatObj(o.RefLaneBlockArea)}_End",
                EnemySet o => $"EST:{o.TagTblValue}",
                ClickSE o => $"CLK",
                _ => string.Empty
            };

            stringHelper.Begin(Previewer);
            var x = -Previewer.ViewWidth / 2;
            var i = 0;
            foreach ((var obj, var c) in group.Select(x => (x.TimelineObject, colors[x.TimelineObject.IDShortName])).OrderBy(x => x.Item2.PackedValue))
            {
                var size = stringHelper.Draw((i == 0 ? string.Empty : " / ") + formatObj(obj), new Vector2(x, y + 12), Vector2.One, 0, 16, c, new(0, 0.5f));
                x += size.X;
                i++;
            }
            stringHelper.End();
        }
    }
}
