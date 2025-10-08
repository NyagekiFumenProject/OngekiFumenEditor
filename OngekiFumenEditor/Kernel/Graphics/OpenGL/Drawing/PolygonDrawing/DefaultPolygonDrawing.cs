using OngekiFumenEditor.Kernel.Graphics.OpenGL.Base;
using OngekiFumenEditor.Kernel.Graphics.OpenGL.Drawing.LineDrawing;
using OpenTK.Graphics.OpenGL;
using System;
using System.ComponentModel.Composition;

namespace OngekiFumenEditor.Kernel.Graphics.OpenGL.Drawing.PolygonDrawing
{
    internal class DefaultPolygonDrawing : CommonOpenGLDrawingBase, IPolygonDrawing, IDisposable
    {
        private readonly DefaultOpenGLShader shader;
        private readonly int vbo;
        private readonly int vao;

        private float[] postData = new float[VertexCount * 6];
        const int VertexByteSize = (2 + 4) * sizeof(float);
        const int VertexCount = 300000;

        private int postVertexCount = 0;
        private IDrawingContext target;
        private Primitive primitive;
        private DefaultOpenGLRenderManagerImpl defaultDrawingManager;

        public int AvailablePostableVertexCount => VertexCount - postVertexCount;

        public DefaultPolygonDrawing(DefaultOpenGLRenderManagerImpl manager) : base(manager)
        {
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
                    GL.BufferData(BufferTarget.ArrayBuffer, VertexByteSize * VertexCount, postData, BufferUsageHint.DynamicDraw);

                    GL.EnableVertexAttribArray(0);
                    GL.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, sizeof(float) * 6, 0);

                    GL.EnableVertexAttribArray(1);
                    GL.VertexAttribPointer(1, 4, VertexAttribPointerType.Float, false, sizeof(float) * 6, sizeof(float) * 2);
                }
                GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
            }
            GL.BindVertexArray(0);
        }


        public void Dispose()
        {
            GL.DeleteVertexArray(vao);
            GL.DeleteBuffer(vbo);
        }

        public void Begin(IDrawingContext target, Primitive primitive = Primitive.TriangleStrip)
        {
            target.PerfomenceMonitor.OnBeginDrawing(this);
            this.target = target;
            this.primitive = primitive;
            shader.Begin();
            shader.PassUniform("Model", GetOverrideModelMatrix());
            shader.PassUniform("ViewProjection", GetOverrideViewProjectMatrixOrDefault(target.CurrentDrawingTargetContext));
            GL.BindVertexArray(vao);
        }

        public void PostPoint(System.Numerics.Vector2 Point, System.Numerics.Vector4 Color)
        {
            if (postVertexCount > VertexCount)
                throw new InvalidOperationException($"postVertexCount > VertexCount({VertexCount})");

            postData[6 * postVertexCount + 0] = Point.X;
            postData[6 * postVertexCount + 1] = Point.Y;
            postData[6 * postVertexCount + 2] = Color.X;
            postData[6 * postVertexCount + 3] = Color.Y;
            postData[6 * postVertexCount + 4] = Color.Z;
            postData[6 * postVertexCount + 5] = Color.W;

            postVertexCount++;
        }

        public void End()
        {
            FlushDraw();

            GL.BindVertexArray(0);
            shader.End();
            target.PerfomenceMonitor.OnAfterDrawing(this);

            target = default;
        }

        private void FlushDraw()
        {
            var glPrimitive = primitive switch
            {
                Primitive.TriangleStrip => PrimitiveType.TriangleStrip,
                Primitive.Triangles => PrimitiveType.Triangles,
                _ => throw new NotSupportedException()
            };
            GL.NamedBufferSubData(vbo, IntPtr.Zero, postVertexCount * VertexByteSize, postData);
            GL.DrawArrays(glPrimitive, 0, postVertexCount);
            target.PerfomenceMonitor.CountDrawCall(this);
            postVertexCount = 0;
        }
    }
}
