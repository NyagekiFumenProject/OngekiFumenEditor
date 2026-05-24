using OngekiFumenEditor.Kernel.Graphics.OpenGL;
using OpenTK.Graphics.OpenGL;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using static OngekiFumenEditor.Kernel.Graphics.ILineDrawing;

namespace OngekiFumenEditor.Kernel.Graphics.OpenGL.Drawing.LineDrawing
{
    internal sealed class DefaultLineDrawing : CommonOpenGLDrawingBase, ILineDrawing, IDisposable
    {
        private const int VertexFloatCount = 2 + 4 + 2;
        private const int VertexByteSize = VertexFloatCount * sizeof(float);
        private const float AaRadius = 1.5f;

        private readonly GeometryPolyLineShader shader;
        private readonly int vbo;
        private readonly int vao;
        private int bufferCapacityInBytes;

        public DefaultLineDrawing(DefaultOpenGLRenderManagerImpl manager) : base(manager)
        {
            shader = new GeometryPolyLineShader();
            shader.Compile();

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
                    GL.EnableVertexAttribArray(0);
                    GL.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, VertexByteSize, 0);

                    GL.EnableVertexAttribArray(1);
                    GL.VertexAttribPointer(1, 4, VertexAttribPointerType.Float, false, VertexByteSize, 2 * sizeof(float));

                    GL.EnableVertexAttribArray(2);
                    GL.VertexAttribPointer(2, 2, VertexAttribPointerType.Float, false, VertexByteSize, 6 * sizeof(float));
                }
                GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
            }
            GL.BindVertexArray(0);
        }

        public void Draw(IDrawingContext target, IEnumerable<LineVertex> points, float lineWidth)
        {
            if (lineWidth <= 0)
                return;

            var viewportWidth = target.CurrentDrawingTargetContext.Rect.Width;
            var viewportHeight = target.CurrentDrawingTargetContext.Rect.Height;
            if (viewportWidth <= 0 || viewportHeight <= 0)
                return;

            {
                var count = UpdateBuffer(points);

                shader.Begin();
                {
                    var mvpMatrix = GetOverrideModelMatrix() * GetOverrideViewProjectMatrixOrDefault(target.CurrentDrawingTargetContext);
                    shader.PassUniform(shader.ModelViewProjectionLocation, mvpMatrix);
                    shader.PassUniform(shader.ViewportSizeLocation, new Vector2(viewportWidth, viewportHeight));
                    shader.PassUniform(shader.LineWidthLocation, lineWidth);
                    shader.PassUniform(shader.AaRadiusLocation, AaRadius);

                    GL.BindVertexArray(vao);
                    {
                        GL.DrawArrays(PrimitiveType.LineStripAdjacency, 0, count);
                        target.RenderContext.PerfomenceMonitor.CountDrawCall();
                    }
                    GL.BindVertexArray(0);
                }
                shader.End();
            }
        }

        private int UpdateBuffer(IEnumerable<LineVertex> points)
        {
            var uploadBuffer = ArrayPool<float>.Shared.Rent(1024);

            void ExtendBufferCapacity(int requiredFloatCount)
            {
                if (uploadBuffer.Length > 0)
                    ArrayPool<float>.Shared.Return(uploadBuffer);

                uploadBuffer = ArrayPool<float>.Shared.Rent(requiredFloatCount);
            }

            void WriteVertex(ref int idx, LineVertex vertex)
            {
                if (idx + 8 > uploadBuffer.Length)
                    ExtendBufferCapacity((idx + 8) * 2);

                uploadBuffer[idx++] = vertex.Point.X;
                uploadBuffer[idx++] = vertex.Point.Y;

                uploadBuffer[idx++] = vertex.Color.X;
                uploadBuffer[idx++] = vertex.Color.Y;
                uploadBuffer[idx++] = vertex.Color.Z;
                uploadBuffer[idx++] = vertex.Color.W;

                uploadBuffer[idx++] = vertex.Dash.DashSize;
                uploadBuffer[idx++] = vertex.Dash.GapSize;
            }

            var vertexCount = 0;
            var bufferIndex = 0;

            if (points is IList<LineVertex> list)
            {
                WriteVertex(ref bufferIndex, list[0]);
                for (var i = 0; i < list.Count; i++)
                    WriteVertex(ref bufferIndex, list[i]);
                WriteVertex(ref bufferIndex, list[list.Count - 1]);
                vertexCount = list.Count + 2;
            }
            else
            {
                var itor = points.GetEnumerator();
                if (itor.MoveNext())
                {
                    WriteVertex(ref bufferIndex, itor.Current);
                    vertexCount++;
                    WriteVertex(ref bufferIndex, itor.Current);
                    vertexCount++;

                    var prev = itor.Current;
                    while (itor.MoveNext())
                    {
                        WriteVertex(ref bufferIndex, itor.Current);
                        vertexCount++;
                        prev = itor.Current;
                    }
                    WriteVertex(ref bufferIndex, prev);
                    vertexCount++;
                }
            }

            var uploadSizeInBytes = vertexCount * VertexByteSize;
            EnsureVboCapacity(uploadSizeInBytes);
            GL.NamedBufferSubData(vbo, IntPtr.Zero, new IntPtr(uploadSizeInBytes), uploadBuffer);

            if (uploadBuffer.Length > 0)
                ArrayPool<float>.Shared.Return(uploadBuffer);
            return vertexCount;
        }

        private void EnsureVboCapacity(int uploadSizeInBytes)
        {
            if (uploadSizeInBytes <= bufferCapacityInBytes)
                return;

            bufferCapacityInBytes = Math.Max(uploadSizeInBytes, bufferCapacityInBytes * 2);
            GL.NamedBufferData(vbo, new IntPtr(bufferCapacityInBytes), IntPtr.Zero, BufferUsageHint.DynamicDraw);
        }

        public void Dispose()
        {
            shader.Dispose();
            GL.DeleteVertexArray(vao);
            GL.DeleteBuffer(vbo);
        }
    }
}
