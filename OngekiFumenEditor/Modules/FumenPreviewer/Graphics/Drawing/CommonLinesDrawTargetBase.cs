using Caliburn.Micro;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Modules.FumenPreviewer.Graphics.Drawing.Shaders;
using OngekiFumenEditor.Utils;
using OngekiFumenEditor.Utils.ObjectPool;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using System;
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

        public IFumenPreviewer Previewer { get; }
        public int LineWidth { get; set; } = 2;

        public CommonLinesDrawTargetBase()
        {
            shader = CommonLineShader.Shared;

            vbo = GL.GenBuffer();

            vao = GL.GenVertexArray();

            Previewer = IoC.Get<IFumenPreviewer>();

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
                        IntPtr.Zero, BufferUsageHint.StreamDraw);

                    GL.EnableVertexAttribArray(0);
                    GL.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, sizeof(float) * 6, 0);

                    GL.EnableVertexAttribArray(1);
                    GL.VertexAttribPointer(1, 4, VertexAttribPointerType.Float, false, sizeof(float) * 6, sizeof(float) * 2);
                }
                GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
            }
            GL.BindVertexArray(0);
        }

        public abstract void FillLine(List<LinePoint> appendFunc, T obj, OngekiFumen fumen);

        public override void Draw(T obj, OngekiFumen fumen)
        {
            GL.LineWidth(LineWidth);
            shader.Begin();
            shader.PassUniform("Model", Matrix4.CreateTranslation(-Previewer.ViewWidth / 2, -Previewer.ViewHeight / 2, 0));
            shader.PassUniform("ViewProjection", Previewer.ViewProjectionMatrix);
            GL.BindVertexArray(vao);
            int i = 0;

            void FlushDraw()
            {
                GL.DrawArrays(PrimitiveType.LineStrip, 0, i);
            }

            unsafe void Copy(LinePoint lp, float* p)
            {
                p[0] = lp.Point.X;
                p[1] = lp.Point.Y;
                p[2] = lp.Color.X;
                p[3] = lp.Color.Y;
                p[4] = lp.Color.Z;
                p[5] = lp.Color.W;
            }

            GL.BindBuffer(BufferTarget.ArrayBuffer, vbo);
            {
                unsafe
                {
                    var ptr = GL.MapBuffer(BufferTarget.ArrayBuffer, BufferAccess.WriteOnly);
                    var p = (float*)ptr.ToPointer();

                    using var d = ObjectPool<List<LinePoint>>.GetWithUsingDisposable(out var list, out _);
                    list.Clear();
                    FillLine(list, obj, fumen);

                    var prevLinePoint = list.Count > 0 ? list[0] : default;

                    foreach (var item in list.SequenceWrap(LINE_DRAW_MAX - 1))
                    {
                        Copy(prevLinePoint, p);
                        p += 6;
                        i++;
                        foreach (var q in item)
                        {
                            Copy(q, p);
                            p += 6;
                            prevLinePoint = q;
                            i++;
                        }

                        GL.UnmapBuffer(BufferTarget.ArrayBuffer);
                        FlushDraw();
                        i = 0;
                        ptr = GL.MapBuffer(BufferTarget.ArrayBuffer, BufferAccess.WriteOnly);
                        p = (float*)ptr.ToPointer();
                    }

                    GL.UnmapBuffer(BufferTarget.ArrayBuffer);
                }
            }
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
