using OngekiFumenEditor.Kernel.Graphics.OpenGL.Base;
using OpenTK.Graphics.OpenGL4;
using System;
using System.Numerics;

namespace OngekiFumenEditor.Kernel.Graphics.OpenGL.Drawing.StringDrawing
{
    /// <summary>
    /// 字形四边形的批量渲染器：把整串文字的字形拼成一个动态顶点缓冲，一次 DrawArrays 画完。
    /// <para>
    /// 颜色约定与 <c>CommonSpriteShader</c> 一致：<c>out_color = texture(diffuse, uv) * color</c>。
    /// 配合「白色 RGB + 覆盖率在 A」的图集与 <c>BlendFuncSeparate(SrcAlpha, OneMinusSrcAlpha, One, OneMinusSrcAlpha)</c>，
    /// 片元输出即 (tint.rgb, tint.a × coverage)，无需 premultiplied 处理。
    /// </para>
    /// </summary>
    internal sealed class SkiaGlyphRenderer : IDisposable
    {
        private const int FloatsPerVertex = 2 + 2 + 4;   // pos.xy + uv + color.rgba
        private const int FloatsPerQuad = FloatsPerVertex * 6;   // 两个三角形，不做索引

        /// <summary>单批次顶点上限（约 2MB）：极端帧不至于把动态缓冲撑成超大数组，超出就由调用方先落盘。</summary>
        private const int MaxBatchFloats = 1 << 19;

        private readonly int vao;
        private readonly int vbo;
        private readonly int mvpLocation;
        private readonly int diffuseLocation;
        private readonly DefaultOpenGLShader shader;

        private float[] vertices = new float[FloatsPerQuad * 64];
        private int vertexFloatCount;

        public SkiaGlyphRenderer()
        {
            shader = new DefaultOpenGLShader
            {
                VertexProgram = @"
                    #version 330
                    uniform mat4 ModelViewProjection;
                    layout(location=0) in vec2 in_pos;
                    layout(location=1) in vec2 in_uv;
                    layout(location=2) in vec4 in_color;
                    out vec2 v_uv;
                    out vec4 v_color;
                    void main(){
                        gl_Position = ModelViewProjection * vec4(in_pos, 0.0, 1.0);
                        v_uv = in_uv;
                        v_color = in_color;
                    }",
                FragmentProgram = @"
                    #version 330
                    uniform sampler2D diffuse;
                    in vec2 v_uv;
                    in vec4 v_color;
                    out vec4 out_color;
                    void main(){
                        out_color = texture(diffuse, v_uv) * v_color;
                    }",
            };
            shader.Compile();
            mvpLocation = shader.GetUniformLocation("ModelViewProjection");
            diffuseLocation = shader.GetUniformLocation("diffuse");

            vao = GL.GenVertexArray();
            vbo = GL.GenBuffer();

            GL.BindVertexArray(vao);
            GL.BindBuffer(BufferTarget.ArrayBuffer, vbo);
            GL.BufferData(BufferTarget.ArrayBuffer, new IntPtr(sizeof(float) * vertices.Length), IntPtr.Zero, BufferUsageHint.StreamDraw);

            var stride = FloatsPerVertex * sizeof(float);
            GL.EnableVertexAttribArray(0);
            GL.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, stride, 0);
            GL.EnableVertexAttribArray(1);
            GL.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, stride, sizeof(float) * 2);
            GL.EnableVertexAttribArray(2);
            GL.VertexAttribPointer(2, 4, VertexAttribPointerType.Float, false, stride, sizeof(float) * 4);

            GL.BindVertexArray(0);
            GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
        }

        /// <summary>本批已收集的顶点是否达到上限，调用方据此先落盘再继续压入。</summary>
        public bool IsFull => vertexFloatCount >= MaxBatchFloats;

        /// <summary>丢弃已收集但未提交的顶点（不产生 GL 调用）；重放之间用它保证批次不跨帧残留。</summary>
        public void Discard() => vertexFloatCount = 0;

        /// <summary>开始收集顶点；<paramref name="mvp"/> 由调用方按「覆盖模型矩阵 × 视图投影」算好。</summary>
        public void Begin(Matrix4x4 mvp)
        {
            vertexFloatCount = 0;

            shader.Begin();
            shader.PassUniform(mvpLocation, mvp);
        }

        /// <summary>压入一个字形矩形（四个角按顺序：左上、右上、右下、左下），uv 为该字形在图集里的矩形。</summary>
        public void PushQuad(Vector2 topLeft, Vector2 topRight, Vector2 bottomRight, Vector2 bottomLeft,
            Vector4 uv, Vector4 color)
        {
            EnsureCapacity(FloatsPerQuad);

            // 两个三角形：TL,TR,BR + TL,BR,BL
            PushVertex(topLeft, uv.X, uv.Y, color);
            PushVertex(topRight, uv.Z, uv.Y, color);
            PushVertex(bottomRight, uv.Z, uv.W, color);
            PushVertex(topLeft, uv.X, uv.Y, color);
            PushVertex(bottomRight, uv.Z, uv.W, color);
            PushVertex(bottomLeft, uv.X, uv.W, color);
        }

        private void PushVertex(Vector2 position, float u, float v, Vector4 color)
        {
            vertices[vertexFloatCount++] = position.X;
            vertices[vertexFloatCount++] = position.Y;
            vertices[vertexFloatCount++] = u;
            vertices[vertexFloatCount++] = v;
            vertices[vertexFloatCount++] = color.X;
            vertices[vertexFloatCount++] = color.Y;
            vertices[vertexFloatCount++] = color.Z;
            vertices[vertexFloatCount++] = color.W;
        }

        private void EnsureCapacity(int extraFloats)
        {
            if (vertexFloatCount + extraFloats <= vertices.Length)
                return;

            Array.Resize(ref vertices, Math.Max(vertices.Length * 2, vertexFloatCount + extraFloats));
        }

        /// <summary>上传并绘制本批字形；之后着色器解绑，顶点收集重新开始。</summary>
        public void End(DefaultOpenGLTexture texture, IDrawingContext target)
        {
            try
            {
                if (vertexFloatCount == 0 || texture is null)
                    return;

                shader.PassUniform(diffuseLocation, texture);

                GL.BindVertexArray(vao);
                GL.BindBuffer(BufferTarget.ArrayBuffer, vbo);
                GL.BufferData(BufferTarget.ArrayBuffer, new IntPtr(sizeof(float) * vertexFloatCount), vertices, BufferUsageHint.StreamDraw);
                GL.BindBuffer(BufferTarget.ArrayBuffer, 0);

                GL.DrawArrays(PrimitiveType.Triangles, 0, vertexFloatCount / FloatsPerVertex);
                target.RenderContext.PerfomenceMonitor.CountDrawCall();

                GL.BindVertexArray(0);
            }
            finally
            {
                vertexFloatCount = 0;
                shader.End();
            }
        }

        public void Dispose()
        {
            GL.DeleteVertexArray(vao);
            GL.DeleteBuffer(vbo);
            shader?.Dispose();
        }
    }
}
