using Caliburn.Micro;
using OngekiFumenEditor.Modules.FumenPreviewer.Graphics.Drawing.DefaultDrawingImpl.StringDrawing.String;
using OpenTK.Graphics.OpenGL;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Windows.Media.Imaging;
using static System.Windows.Forms.AxHost;

namespace OngekiFumenEditor.Modules.FumenPreviewer.Graphics.Drawing.DefaultDrawingImpl.TextureDrawing
{
    [Export(typeof(ITextureDrawing))]
    [Export(typeof(IBatchTextureDrawing))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    internal class DefaultBatchTextureDrawing : CommonDrawingBase, IBatchTextureDrawing, IDisposable
    {
        private IPerfomenceMonitor performenceMonitor;
        private BatchShader shader;
        private static byte[] postData;
        private static int vboVertexBase, vboTexPosBase;
        private int currentPostBaseIndex = 0;
        public int currentPostCount  = 0;
        private static int[] vaoArr = new int[BUFFER_COUNT];
        private static int[] vboArr = new int[BUFFER_COUNT];
        private int curBufferIdx = 0;
        private static float[] cacheBaseVertex = new float[] {
                -0.5f, 0.5f,
                 0.5f, 0.5f,
                 0.5f, -0.5f,
                -0.5f, -0.5f,
        };
        private static float[] cacheBaseTexPos = new float[] {
                 0,0,
                 1,0,
                 1,1,
                 0,1
        };
        private IFumenPreviewer target;
        private Texture texture;

        /*-----------------CURRENT VERSION------------------ -
                                        modelMatrix(float)
                                        Matrix4*4(16)
        */
        private const int VertexSize = 4 * 4 * sizeof(float);
        private const int MAX_DRAW_COUNT = 3000;
        private const int BUFFER_COUNT = 1;

        public DefaultBatchTextureDrawing()
        {
            performenceMonitor = IoC.Get<IPerfomenceMonitor>();

            shader = new BatchShader();
            shader.Compile();

            postData = new byte[VertexSize * MAX_DRAW_COUNT];

            vboVertexBase = GL.GenBuffer();
            vboTexPosBase = GL.GenBuffer();

            GL.GenVertexArrays(BUFFER_COUNT, vaoArr);
            GL.GenBuffers(BUFFER_COUNT, vboArr);

            for (int i = 0; i < BUFFER_COUNT; i++)
            {
                _InitVertexBase(vaoArr[i]);
                _InitBuffer(vaoArr[i], vboArr[i]);
            }
        }

        private static void _InitVertexBase(int vao)
        {
            GL.BindVertexArray(vao);
            {
                GL.BindBuffer(BufferTarget.ArrayBuffer, vboTexPosBase);
                {
                    //绑定基本顶点
                    GL.BufferData(BufferTarget.ArrayBuffer, new IntPtr(sizeof(float) * cacheBaseTexPos.Length),
                        cacheBaseTexPos, BufferUsageHint.StaticDraw);

                    GL.EnableVertexAttribArray(0);
                    GL.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, sizeof(float) * (2), 0);
                    GL.VertexAttribDivisor(0, 0);
                }
                GL.BindBuffer(BufferTarget.ArrayBuffer, 0);

                GL.BindBuffer(BufferTarget.ArrayBuffer, vboVertexBase);
                {
                    //绑定基本顶点
                    GL.BufferData(BufferTarget.ArrayBuffer, new IntPtr(sizeof(float) * cacheBaseVertex.Length),
                        cacheBaseVertex, BufferUsageHint.StaticDraw);

                    GL.EnableVertexAttribArray(1);
                    GL.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, sizeof(float) * (2), 0);
                    GL.VertexAttribDivisor(1, 0);
                }
                GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
            }
            GL.BindVertexArray(0);
        }

        private static void _InitBuffer(int vao, int vbo)
        {
            GL.BindVertexArray(vao);
            {
                GL.BindBuffer(BufferTarget.ArrayBuffer, vbo);
                {
                    GL.BufferData(BufferTarget.ArrayBuffer, new IntPtr(VertexSize * MAX_DRAW_COUNT), IntPtr.Zero, BufferUsageHint.DynamicDraw);

                    //ModelMatrix
                    GL.EnableVertexAttribArray(2);
                    var strip = 0;
                    GL.VertexAttribPointer(2, 4, VertexAttribPointerType.Float, false, VertexSize, strip);
                    GL.VertexAttribDivisor(2, 1);

                    GL.EnableVertexAttribArray(3);
                    strip += 4 * sizeof(float);
                    GL.VertexAttribPointer(3, 4, VertexAttribPointerType.Float, false, VertexSize, strip);
                    GL.VertexAttribDivisor(3, 1);

                    GL.EnableVertexAttribArray(4);
                    strip += 4 * sizeof(float);
                    GL.VertexAttribPointer(4, 4, VertexAttribPointerType.Float, false, VertexSize, strip);
                    GL.VertexAttribDivisor(4, 1);

                    GL.EnableVertexAttribArray(5);
                    strip += 4 * sizeof(float);
                    GL.VertexAttribPointer(5, 4, VertexAttribPointerType.Float, false, VertexSize, strip);
                    GL.VertexAttribDivisor(5, 1);
                }
                GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
            }
            GL.BindVertexArray(0);
        }

        private void Draw()
        {
            if (currentPostCount == 0)
                return;
            shader.Begin();

            var MVP =
                    GetOverrideModelMatrix() * target.ViewProjectionMatrix;
            shader.PassUniform("ViewProjection", MVP);
            shader.PassUniform("diffuse", texture);

            GL.NamedBufferSubData(vboArr[curBufferIdx], (IntPtr)(0), (IntPtr)(VertexSize * currentPostCount), postData);

            GL.BindVertexArray(vaoArr[curBufferIdx]);
            {
                GL.DrawArraysInstanced(PrimitiveType.TriangleFan, 0, 4, currentPostCount);
                performenceMonitor.CountDrawCall(this);
            }
            GL.BindVertexArray(0);
            curBufferIdx = (curBufferIdx + 1) % vboArr.Length;
        }

        private void FlushDraw()
        {
            Draw();
            Clear();
        }

        private void Clear()
        {
            currentPostCount = 0;
            currentPostBaseIndex = 0;
        }

        public void Dispose()
        {
            GL.DeleteBuffer(vboVertexBase);
            GL.DeleteBuffer(vboTexPosBase);
            GL.DeleteBuffers(BUFFER_COUNT, vboArr);
            GL.DeleteVertexArrays(BUFFER_COUNT, vaoArr);
        }

        public void Draw(IFumenPreviewer target, Texture texture, IEnumerable<(Vector2 size, Vector2 position, float rotation)> instances)
        {
            Begin(target, texture);
            foreach ((Vector2 size, Vector2 position, float rotation) in instances)
                PostSprite(size, position, rotation);
            FlushDraw();
            End();
        }

        public void Begin(IFumenPreviewer target, Texture texture)
        {
            performenceMonitor.OnBeginDrawing(this);
            this.target = target;
            this.texture = texture;
        }

        public void PostSprite(Vector2 size, Vector2 position, float rotation)
        {
            /*-----------------CURRENT VERSION------------------ -
			*     modelMatrix
			*     Matrix4x4(16)
			*/

            var modelMatrix =
                    OpenTK.Mathematics.Matrix4.CreateScale(new OpenTK.Mathematics.Vector3(texture.Width, texture.Height, 1)) *
                    OpenTK.Mathematics.Matrix4.CreateScale(new OpenTK.Mathematics.Vector3(size.X / texture.Width, size.Y / texture.Height, 1)) *
                    OpenTK.Mathematics.Matrix4.CreateRotationZ(rotation) *
                    OpenTK.Mathematics.Matrix4.CreateTranslation(position.X, position.Y, 0);

            unsafe
            {
                //copy matrix4 to buffer
                fixed (byte* ptr = &postData[currentPostBaseIndex])
                {
                    var copyLen = 4 * 4 * sizeof(float);
                    var basePtr = (byte*)&modelMatrix.Row0.X;
                    for (int i = 0; i < copyLen; i++)
                        ptr[i] = *(basePtr + i);
                }
            }

            currentPostCount++;
            currentPostBaseIndex += VertexSize;
            if (currentPostCount >= MAX_DRAW_COUNT)
                FlushDraw();
        }

        public void End()
        {
            FlushDraw();
            target = default;
            texture = default;
            performenceMonitor.OnAfterDrawing(this);
        }
    }
}
