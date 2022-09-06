using Caliburn.Micro;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Modules.FumenPreviewer.Graphics.Drawing.Shaders;
using OngekiFumenEditor.Utils;
using OngekiFumenEditor.Utils.ObjectPool;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Modules.FumenPreviewer.Graphics.Drawing
{
    public abstract class CommonLinesDrawTargetBase<T> : CommonDrawTargetBase<T>, IDisposable where T : OngekiObjectBase
    {
        public record LinePoint(Vector2 Point, Vector4 Color);

        private readonly Shader shader;
        private readonly int vbo;
        private readonly int vao;

        public const int LINE_DRAW_MAX = 100;

        public int LineWidth { get; set; } = 2;

        public CommonLinesDrawTargetBase()
        {
            shader = CommonLineShader.Shared;

            vbo = GL.GenBuffer();
            vao = GL.GenVertexArray();

            Init();

            GL.Enable(EnableCap.LineSmooth);
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

        public abstract void FillLine(List<LinePoint> list, T obj, OngekiFumen fumen);

        public override void Draw(T obj, OngekiFumen fumen)
        {
            using var d = ObjectPool<List<LinePoint>>.GetWithUsingDisposable(out var list, out _);
            list.Clear();
            FillLine(list, obj, fumen);

            Draw(list, LineWidth);
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
                /*
                p[0] = lp.Point.X;
                p[1] = lp.Point.Y;
                p[2] = lp.Color.X;
                p[3] = lp.Color.Y;
                p[4] = lp.Color.Z;
                p[5] = lp.Color.W;
                */
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
    }
}
