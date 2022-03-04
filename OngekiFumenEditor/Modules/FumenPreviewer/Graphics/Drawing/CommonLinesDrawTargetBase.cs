using Caliburn.Micro;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Modules.FumenPreviewer.Graphics.Drawing.Shaders;
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

        public const int LINE_DRAW_MAX = 50;

        public IFumenPreviewer Previewer { get; }
        public int LineWidth { get; set; } = 2;

        public CommonLinesDrawTargetBase()
        {
            shader = CommonLineShader.Shared;

            vbo = GL.GenBuffer();

            vao = GL.GenVertexArray();

            Previewer = IoC.Get<IFumenPreviewer>();

            Init();
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

        public abstract void FillLine(Action<Vector2, Vector4> appendFunc);

        public override void Draw(T ongekiObject, OngekiFumen fumen)
        {
            int i = 0;

            GL.BindBuffer(BufferTarget.ArrayBuffer, vbo);
            {
                unsafe
                {
                    var ptr = GL.MapBuffer(BufferTarget.ArrayBuffer, BufferAccess.WriteOnly);
                    var p = (float*)ptr.ToPointer();
                    FillLine((pos, color) =>
                    {
                        p[6 * i + 0] = pos.X;
                        p[6 * i + 1] = pos.Y;
                        p[6 * i + 2] = color.X;
                        p[6 * i + 3] = color.Y;
                        p[6 * i + 4] = color.Z;
                        p[6 * i + 5] = color.W;

                        i++;
                    });
                    GL.UnmapBuffer(BufferTarget.ArrayBuffer);
                }
            }

            GL.LineWidth(LineWidth);
            shader.Begin();
            shader.PassUniform("Model", Matrix4.CreateTranslation(-Previewer.ViewWidth / 2, -Previewer.ViewHeight / 2, 0));
            shader.PassUniform("ViewProjection", Previewer.ViewProjectionMatrix);
            GL.BindVertexArray(vao);
            {
                GL.DrawArrays(PrimitiveType.LineStrip, 0, i);
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
