using FontStashSharp;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Modules.FumenPreviewer.Graphics.Drawing.Base;
using OngekiFumenEditor.Modules.FumenPreviewer.Graphics.Drawing.Shaders;
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

namespace OngekiFumenEditor.Modules.FumenPreviewer.Graphics.Drawing.TargetImpl
{
    [Export(typeof(IDrawingTarget))]
    public class HorizonalDrawingTarget : CommonDrawTargetBase<OngekiTimelineObjectBase>, IDisposable
    {
        public record RegisterDrawingInfo(OngekiTimelineObjectBase TimelineObject, double Y);

        private readonly Shader shader;
        private readonly int vbo;
        private readonly int vao;
        private bool backup_ps;
        private int backup_ps_hint;
        private HashSet<RegisterDrawingInfo> registeredObjects = new();
        private DrawStringHelper stringHelper;

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

        public void Draw(List<LinePoint> list, int lineWidth)
        {
            if (list.Count == 0)
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
                GL.DrawArrays(PrimitiveType.LineStrip, 0, arrBufferIdx);
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
        }

        private void DrawInternal(IEnumerable<RegisterDrawingInfo> objects)
        {
            foreach (var group in objects.GroupBy(x => x.TimelineObject.TGrid.TotalGrid))
            {
                var y = (float)group.FirstOrDefault().Y;
                //todo 换个姿势优化
                if ((y < Previewer.CurrentPlayTime) || y > (Previewer.CurrentPlayTime + Previewer.ViewHeight))
                    continue;

                using var d = group.Select(x => colors[x.TimelineObject.IDShortName]).OrderBy(x => x.PackedValue).ToListWithObjectPool(out var regColors);
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


                DrawDescText(y, group);
            }
        }

        private void DrawDescText(float y, IGrouping<int, RegisterDrawingInfo> group)
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
