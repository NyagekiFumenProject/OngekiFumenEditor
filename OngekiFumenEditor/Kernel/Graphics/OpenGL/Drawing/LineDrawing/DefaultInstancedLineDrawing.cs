/*
Code copied and modified from https://github.com/mhalber/Lines/blob/master/instancing_lines.h
*/

using OngekiFumenEditor.Kernel.Graphics.OpenGL;
using OngekiFumenEditor.Kernel.Graphics.OpenGL.Base;
using OngekiFumenEditor.Utils;
using OpenTK.Graphics.OpenGL;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using static OngekiFumenEditor.Kernel.Graphics.ILineDrawing;
using static OngekiFumenEditor.Kernel.Graphics.IStaticVBODrawing;

namespace OngekiFumenEditor.Kernel.Graphics.OpenGL.Drawing.LineDrawing
{
    internal sealed class DefaultInstancedLineDrawing : CommonOpenGLDrawingBase, ISimpleLineDrawing, IDisposable
    {
        public const int MAX_VERTS = 300000;
        /*
            typedef struct vertex
            {
                union
                {
                    struct { msh_vec3_t pos; float width; };
                    msh_vec4_t pos_width;
                };
                msh_vec4_t col;
            } vertex_t;
         */
        public const int VertexTBytesSize = sizeof(float) * (3 + 1 + 4 + 2);

        private int vao;
        private int line_vbo;
        private int quad_vbo;
        private int quad_ebo;

        private DefaultOpenGLShader shader;

        private int quad_pos;
        private int line_pos_width_a;
        private int line_col_a;
        private int line_pos_width_b;
        private int line_col_b;

        private int mvp_loc;
        private int viewport_size_loc;
        private int dashSize;
        private int gapSize;
        private int aa_radius_loc;

        private float[] PostData { get; } = new float[MAX_VERTS];
        private int postDataFillIndex = 0;
        private int postDataFillCount = 0;
        private IDrawingContext target;
        private float lineWidth;

        private struct VertexData
        {
            public float x;
            public float y;
            public float z;

            public float lineWidth;

            public float r;
            public float g;
            public float b;
            public float a;

            public float dashSize;
            public float gapSize;
        }

        private OpenTK.Mathematics.Vector2 aa_radius_val = new OpenTK.Mathematics.Vector2(2, 2);

        public DefaultInstancedLineDrawing(DefaultOpenGLRenderManagerImpl manager) : base(manager)
        {
            shader = new InstancedLineShader();
            shader.Compile();

            quad_pos = shader.GetAttribLocation("quad_pos");
            line_pos_width_a = shader.GetAttribLocation("line_pos_width_a");
            line_col_a = shader.GetAttribLocation("line_col_a");
            line_pos_width_b = shader.GetAttribLocation("line_pos_width_b");
            line_col_b = shader.GetAttribLocation("line_col_b");

            mvp_loc = shader.GetUniformLocation("u_mvp");
            aa_radius_loc = shader.GetUniformLocation("u_aa_radius");
            viewport_size_loc = shader.GetUniformLocation("u_viewport_size");

            dashSize = shader.GetAttribLocation("dashSize");
            gapSize = shader.GetAttribLocation("gapSize");

            int binding_idx = 0;

            vao = GL.GenVertexArray();

            GL.BindVertexArray(vao);
            {
                line_vbo = GL.GenBuffer();
                GL.BindBuffer(BufferTarget.ArrayBuffer, line_vbo);
                {
                    GL.NamedBufferStorage(line_vbo, PostData.Length * sizeof(float), IntPtr.Zero, BufferStorageFlags.DynamicStorageBit);
                    SetupLineVertexAttributes();
                    SwitchVertexVBO(line_vbo);
                }
                GL.BindBuffer(BufferTarget.ArrayBuffer, 0);

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

        private void FlushDraw()
        {
            if (postDataFillCount > 1)
            {
                GL.NamedBufferSubData(line_vbo, IntPtr.Zero, postDataFillIndex, PostData);

                GL.DrawElementsInstanced(PrimitiveType.Triangles, 6, DrawElementsType.UnsignedShort, IntPtr.Zero, postDataFillCount - 1);
                target.RenderContext.PerfomenceMonitor.CountDrawCall();

                postDataFillIndex = postDataFillCount = 0;
            }
        }

        public void Dispose()
        {
            shader.Dispose();

            GL.DeleteBuffer(line_vbo);
            GL.DeleteBuffer(quad_vbo);
            GL.DeleteBuffer(quad_ebo);
            GL.DeleteVertexArray(vao);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void PostPointInternal(Vector2 point, Vector4 color, VertexDash dash)
        {
            ref var slot = ref MemoryMarshal.Cast<float, VertexData>(PostData.AsSpan())[postDataFillCount];
            slot.x = point.X;
            slot.y = point.Y;
            slot.z = 0;
            slot.lineWidth = lineWidth;
            slot.r = color.X;
            slot.g = color.Y;
            slot.b = color.Z;
            slot.a = color.W;
            slot.dashSize = dash.DashSize;
            slot.gapSize = dash.GapSize;

            postDataFillIndex += VertexTBytesSize;
            postDataFillCount++;

            prevPoint = point;
            prevColor = color;
            prevDash = dash;
        }

        Vector2 prevPoint = default;
        Vector4 prevColor = default;
        VertexDash prevDash = default;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void PostPoint(Vector2 Point, Vector4 Color, VertexDash dash)
        {
            if (postDataFillIndex + VertexTBytesSize >= MAX_VERTS)
            {
                FlushDraw();
                PostPointInternal(prevPoint, prevColor, prevDash);
            }

            PostPointInternal(Point, Color, dash);
        }

        public void End()
        {
            FlushDraw();

            GL.BindVertexArray(0);
            shader.End();

        }

        public void Begin(IDrawingContext target, float lineWidth)
        {
            this.target = target;
            this.lineWidth = lineWidth;
            postDataFillIndex = postDataFillCount = 0;

            shader.Begin();
            GL.BindVertexArray(vao);
            var mvpMatrix = GetOverrideModelMatrix() * GetOverrideViewProjectMatrixOrDefault(target.CurrentDrawingTargetContext);
            shader.PassUniform(mvp_loc, mvpMatrix);
            shader.PassUniform(viewport_size_loc, new Vector2(target.CurrentDrawingTargetContext.Rect.Width, target.CurrentDrawingTargetContext.Rect.Height));
            shader.PassUniform(aa_radius_loc, aa_radius_val);
        }

        public void Draw(IDrawingContext target, IEnumerable<LineVertex> points, float lineWidth)
        {
            Begin(target, lineWidth);

            if (points is IList<LineVertex> list)
            {
                DrawList(target, list, lineWidth);
            }
            else
            {
                foreach (var point in points)
                    PostPoint(point.Point, point.Color, point.Dash);
            }
            End();
        }

        private void DrawList(IDrawingContext target, IList<LineVertex> list, float lineWidth)
        {
            var count = list.Count;

            const int verticesPerBuffer = MAX_VERTS / VertexTBytesSize;
            var meshSpan = MemoryMarshal.Cast<float, VertexData>(PostData.AsSpan());

            var processed = 0;
            while (processed < count)
            {
                var remaining = verticesPerBuffer - postDataFillCount;
                if (remaining <= 0)
                {
                    FlushDraw();
                    PostPointInternal(prevPoint, prevColor, prevDash);
                    remaining = verticesPerBuffer - postDataFillCount;
                }

                var chunk = Math.Min(remaining, count - processed);

                for (var i = 0; i < chunk; i++)
                {
                    var v = list[processed + i];
                    ref var slot = ref meshSpan[postDataFillCount + i];
                    slot.x = v.Point.X;
                    slot.y = v.Point.Y;
                    slot.z = 0;
                    slot.lineWidth = lineWidth;
                    slot.r = v.Color.X;
                    slot.g = v.Color.Y;
                    slot.b = v.Color.Z;
                    slot.a = v.Color.W;
                    slot.dashSize = v.Dash.DashSize;
                    slot.gapSize = v.Dash.GapSize;
                }

                postDataFillCount += chunk;
                postDataFillIndex += chunk * VertexTBytesSize;
                processed += chunk;

                var last = list[processed - 1];
                prevPoint = last.Point;
                prevColor = last.Color;
                prevDash = last.Dash;
            }
        }

        private class VBOHandle : IVBOHandle
        {
            public int vbo = int.MinValue;
            public int verticsCount = 0;

            public void Dispose()
            {
                if (vbo == int.MinValue)
                    return;
                GL.DeleteBuffer(vbo);
                vbo = int.MinValue;
                verticsCount = default;
            }
        }

        public void DrawVBO(IDrawingContext target, IVBOHandle h)
        {
            Begin(target, 1);
            {
                var handle = (VBOHandle)h;

                SwitchVertexVBO(handle.vbo);

                GL.DrawElementsInstanced(PrimitiveType.Triangles, 6, DrawElementsType.UnsignedShort, IntPtr.Zero, handle.verticsCount - 1);
                target.RenderContext.PerfomenceMonitor.CountDrawCall();

                SwitchVertexVBO(line_vbo);
            }
            End();
        }

        private void SwitchVertexVBO(int vbo)
        {
            GL.VertexArrayVertexBuffer(vao, 0, vbo, IntPtr.Zero, VertexTBytesSize);
        }

        private void SetupLineVertexAttributes()
        {
            GL.VertexArrayBindingDivisor(vao, 0, 1);

            GL.EnableVertexArrayAttrib(vao, line_pos_width_a);
            GL.VertexArrayAttribFormat(vao, line_pos_width_a, 4, VertexAttribType.Float, false, 0);
            GL.VertexArrayAttribBinding(vao, line_pos_width_a, 0);

            GL.EnableVertexArrayAttrib(vao, line_col_a);
            GL.VertexArrayAttribFormat(vao, line_col_a, 4, VertexAttribType.Float, false, 4 * sizeof(float));
            GL.VertexArrayAttribBinding(vao, line_col_a, 0);

            GL.EnableVertexArrayAttrib(vao, line_pos_width_b);
            GL.VertexArrayAttribFormat(vao, line_pos_width_b, 4, VertexAttribType.Float, false, VertexTBytesSize);
            GL.VertexArrayAttribBinding(vao, line_pos_width_b, 0);

            GL.EnableVertexArrayAttrib(vao, line_col_b);
            GL.VertexArrayAttribFormat(vao, line_col_b, 4, VertexAttribType.Float, false, VertexTBytesSize + 4 * sizeof(float));
            GL.VertexArrayAttribBinding(vao, line_col_b, 0);

            GL.EnableVertexArrayAttrib(vao, dashSize);
            GL.VertexArrayAttribFormat(vao, dashSize, 1, VertexAttribType.Float, false, 8 * sizeof(float));
            GL.VertexArrayAttribBinding(vao, dashSize, 0);

            GL.EnableVertexArrayAttrib(vao, gapSize);
            GL.VertexArrayAttribFormat(vao, gapSize, 1, VertexAttribType.Float, false, 9 * sizeof(float));
            GL.VertexArrayAttribBinding(vao, gapSize, 0);
        }

        public IVBOHandle GenerateVBOWithPresetPoints(IEnumerable<LineVertex> vertices, float lineWidth)
        {
            var handle = new VBOHandle()
            {
                vbo = GL.GenBuffer()
            };
            var vbo = handle.vbo;

            using var list = vertices.ToListWithObjectPool();
            var buffer = ArrayPool<float>.Shared.Rent(list.Count * VertexTBytesSize / sizeof(float));
            var i = 0;

            // A2: iterate the materialized list, not `vertices`. The previous code re-enumerated
            // the source IEnumerable, which double-walks LINQ chains and (worse) lets
            // `i / VertexTBytesSize` drift from `list.Count` if the second pass yields a
            // different element count, mismatching the buffer payload with `handle.verticsCount`.
            foreach (var vertex in list)
            {
                buffer[i++] = vertex.Point.X;
                buffer[i++] = vertex.Point.Y;
                buffer[i++] = 0;

                buffer[i++] = lineWidth;

                buffer[i++] = vertex.Color.X;
                buffer[i++] = vertex.Color.Y;
                buffer[i++] = vertex.Color.Z;
                buffer[i++] = vertex.Color.W;

                buffer[i++] = vertex.Dash.DashSize;
                buffer[i++] = vertex.Dash.GapSize;
            }

            handle.verticsCount = list.Count;

            // A3: NamedBufferData is a DSA call addressing `vbo` directly; no ArrayBuffer bind needed.
            GL.NamedBufferData(vbo, i * sizeof(float), buffer, BufferUsageHint.StaticDraw);
            ArrayPool<float>.Shared.Return(buffer);

            return handle;
        }
    }
}
