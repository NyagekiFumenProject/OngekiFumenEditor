using Caliburn.Micro;
using MahApps.Metro.Controls;
using OngekiFumenEditor.Modules.FumenPreviewer.Graphics.Drawing.DefaultDrawingImpl.StringDrawing.String;
using OpenTK.Compute.OpenCL;
using OpenTK.Graphics.OpenGL;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Markup;
using static System.Windows.Forms.AxHost;
using System.Windows.Media.TextFormatting;
using ControlzEx.Standard;
using OngekiFumenEditor.Utils;

namespace OngekiFumenEditor.Modules.FumenPreviewer.Graphics.Drawing.DefaultDrawingImpl.LineDrawing
{
    [Export(typeof(ILineDrawing))]
    internal class DefaultInstancedLineDrawing : ILineDrawing, IDisposable
    {
        public const int MAX_VERTS = 3 * 12 * 1024;
        public const int VertexTBytesSize = sizeof(float) * (3 + 1 + 4);

        private int vao;
        private int line_vbo;
        private int quad_vbo;
        private int quad_ebo;

        private IPerfomenceMonitor perfomenceMonitor;
        private Shader shader;

        private int quad_pos;
        private int line_pos_width_a;
        private int line_col_a;
        private int line_pos_width_b;
        private int line_col_b;

        private int mvp;
        private int viewport_size;
        private int aa_radius;

        private float[] PostData { get; } = new float[MAX_VERTS];
        private int postDataFillIndex = 0;
        private int postDataFillCount = 0;

        public DefaultInstancedLineDrawing()
        {
            perfomenceMonitor = IoC.Get<IPerfomenceMonitor>();

            shader = new InstancedLineShader();
            shader.Compile();

            quad_pos = shader.GetAttribLocation("quad_pos");
            line_pos_width_a = shader.GetAttribLocation("line_pos_width_a");
            line_col_a = shader.GetAttribLocation("line_col_a");
            line_pos_width_b = shader.GetAttribLocation("line_pos_width_b");
            line_col_b = shader.GetAttribLocation("line_col_b");

            mvp = shader.GetUniformLocation("u_mvp");
            aa_radius = shader.GetUniformLocation("u_aa_radius");
            viewport_size = shader.GetUniformLocation("u_viewport_size");

            int binding_idx = 0;

            vao = GL.GenVertexArray();

            GL.BindVertexArray(vao);
            {
                line_vbo = GL.GenBuffer();
                GL.BindBuffer(BufferTarget.ArrayBuffer, line_vbo);
                {
                    GL.NamedBufferStorage(line_vbo, PostData.Length * sizeof(float), IntPtr.Zero, BufferStorageFlags.DynamicStorageBit);
                }
                GL.VertexArrayVertexBuffer(vao, binding_idx, line_vbo, IntPtr.Zero, 2 * VertexTBytesSize);
                GL.VertexArrayBindingDivisor(vao, binding_idx, 1);

                GL.EnableVertexArrayAttrib(vao, line_pos_width_a);
                GL.VertexArrayAttribFormat(vao, line_pos_width_a, 4, VertexAttribType.Float, false, 0);
                GL.VertexArrayAttribBinding(vao, line_pos_width_a, binding_idx);

                GL.EnableVertexArrayAttrib(vao, line_col_a);
                GL.VertexArrayAttribFormat(vao, line_col_a, 4, VertexAttribType.Float, false, 4 * sizeof(float));
                GL.VertexArrayAttribBinding(vao, line_col_a, binding_idx);

                GL.EnableVertexArrayAttrib(vao, line_pos_width_b);
                GL.VertexArrayAttribFormat(vao, line_pos_width_b, 4, VertexAttribType.Float, false, VertexTBytesSize + 0);
                GL.VertexArrayAttribBinding(vao, line_pos_width_b, binding_idx);

                GL.EnableVertexArrayAttrib(vao, line_col_b);
                GL.VertexArrayAttribFormat(vao, line_col_b, 4, VertexAttribType.Float, false, VertexTBytesSize + 4 * sizeof(float));
                GL.VertexArrayAttribBinding(vao, line_col_b, binding_idx);

                binding_idx++;

                // Figure out this parametrization.
                var quad = new[]{
                    0.0f, -1.0f, 0.0f,
                    0.0f, 1.0f, 0.0f,
                    1.0f, 1.0f, 0.0f,
                    1.0f, -1.0f, 0.0f  };
                var ind = new ushort[] { 0, 1, 2, 0, 2, 3 };


                quad_vbo = GL.GenBuffer();
                quad_ebo = GL.GenBuffer();
                GL.BindBuffer(BufferTarget.ArrayBuffer, quad_vbo);
                {
                    GL.NamedBufferStorage(quad_vbo, quad.Length * sizeof(float), quad, BufferStorageFlags.DynamicStorageBit);
                }
                GL.BindBuffer(BufferTarget.ArrayBuffer, quad_ebo);
                {
                    GL.NamedBufferStorage(quad_ebo, ind.Length * sizeof(ushort), ind, BufferStorageFlags.DynamicStorageBit);
                }
                GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
                GL.VertexArrayVertexBuffer(vao, binding_idx, quad_vbo, IntPtr.Zero, 3 * sizeof(float));
                GL.VertexArrayElementBuffer(vao, quad_ebo);

                GL.EnableVertexArrayAttrib(vao, quad_pos);
                GL.VertexArrayAttribFormat(vao, quad_pos, 3, VertexAttribType.Float, false, 0);
                GL.VertexArrayAttribBinding(vao, quad_pos, binding_idx);
            }
            GL.BindVertexArray(0);
        }

        private unsafe void AppendPoint(ILineDrawing.LineVertex point, float lineWidth)
        {
            var buffer = PostData.AsSpan().Slice(postDataFillIndex / sizeof(float), VertexTBytesSize / sizeof(float));

            buffer[0] = point.Point.X;
            buffer[1] = point.Point.Y;
            buffer[2] = 0;

            buffer[3] = lineWidth;

            buffer[4] = point.Color.X;
            buffer[5] = point.Color.Y;
            buffer[6] = point.Color.Z;
            buffer[7] = point.Color.W;

            postDataFillIndex += VertexTBytesSize;
            postDataFillCount++;
        }

        private void FlushDraw()
        {
            GL.NamedBufferSubData(line_vbo, IntPtr.Zero, postDataFillIndex, PostData);

            GL.DrawElementsInstanced(PrimitiveType.Triangles, 6, DrawElementsType.UnsignedShort, IntPtr.Zero, postDataFillCount >> 1);
            perfomenceMonitor.CountDrawCall(this);
            Log.LogDebug($"postDataFillCount = {postDataFillCount}");

            postDataFillIndex = postDataFillCount = 0;
        }

        public void Draw(IFumenPreviewer target, IEnumerable<ILineDrawing.LineVertex> points, float lineWidth)
        {
            perfomenceMonitor.OnBeginDrawing(this);

            shader.Begin();
            {
                var mvpMatrix = OpenTK.Mathematics.Matrix4.CreateTranslation(-target.ViewWidth / 2, -target.ViewHeight / 2, 0) * target.ViewProjectionMatrix;
                shader.PassUniform(mvp, mvpMatrix);
                shader.PassUniform(viewport_size, new OpenTK.Mathematics.Vector2(target.ViewWidth, target.ViewHeight));
                shader.PassUniform(aa_radius, new OpenTK.Mathematics.Vector2(2, 2));

                var itor = points.GetEnumerator();
                itor.MoveNext();
                var prevPoint = itor.Current;

                void appendCore(ILineDrawing.LineVertex point)
                {
                    if (postDataFillIndex + VertexTBytesSize >= MAX_VERTS)
                    {
                        FlushDraw();
                        AppendPoint(prevPoint, lineWidth);
                    }

                    AppendPoint(point, lineWidth);
                }

                GL.BindVertexArray(vao);
                {
                    while (itor.MoveNext())
                    {
                        var point = itor.Current;
                        appendCore(prevPoint);
                        appendCore(point);
                        prevPoint = point;
                    }

                    FlushDraw();
                }
                GL.BindVertexArray(0);
            }
            shader.End();

            perfomenceMonitor.OnAfterDrawing(this);
        }

        public void Dispose()
        {
            shader.Dispose();

            GL.DeleteBuffer(line_vbo);
            GL.DeleteBuffer(quad_vbo);
            GL.DeleteBuffer(quad_ebo);
            GL.DeleteVertexArray(vao);
        }
    }
}
