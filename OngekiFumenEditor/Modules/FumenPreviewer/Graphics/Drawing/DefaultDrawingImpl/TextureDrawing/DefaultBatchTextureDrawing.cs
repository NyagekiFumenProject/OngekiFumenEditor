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

namespace OngekiFumenEditor.Modules.FumenPreviewer.Graphics.Drawing.DefaultDrawingImpl.TextureDrawing
{
    [Export(typeof(IBatchTextureDrawing))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    internal class DefaultBatchTextureDrawing : IBatchTextureDrawing, IDisposable
    {
        private IPerfomenceMonitor performenceMonitor;
        private BatchShader s_shader;
        public uint Capacity { get; protected set; } = 0;
        private static byte[] PostData;
        private static int s_vbo_vertexBase, s_vbo_texPosBase;
        private const int BUFFER_COUNT = 3;
        private int _currentPostBaseIndex = 0;
        public int CurrentPostCount { get; private set; } = 0;
        private static int[] s_vaos = new int[BUFFER_COUNT];
        private static int[] s_vbos = new int[BUFFER_COUNT];
        private int _current_buffer_index = 0;
        private static float[] _cacheBaseVertex = new float[] {
                -0.5f, 0.5f,
                 0.5f, 0.5f,
                 0.5f, -0.5f,
                -0.5f, -0.5f,
        };
        private static float[] _cacheBaseTexPos = new float[] {
                 0,0,
                 1,0,
                 1,1,
                 0,1
        };

        /*-----------------CURRENT VERSION------------------ -
                                        modelMatrix(float)
                                        Matrix4*4(16)
        */
        private const int _VertexSize = 4 * 4 * sizeof(float);
        private const int _DrawCallInstanceCountMax = 300;

        public DefaultBatchTextureDrawing()
        {
            performenceMonitor = IoC.Get<IPerfomenceMonitor>();

            s_shader = new BatchShader();
            s_shader.Compile();

            PostData = new byte[_VertexSize * _DrawCallInstanceCountMax];

            s_vbo_vertexBase = GL.GenBuffer();
            s_vbo_texPosBase = GL.GenBuffer();

            GL.GenVertexArrays(BUFFER_COUNT, s_vaos);
            GL.GenBuffers(BUFFER_COUNT, s_vbos);

            for (int i = 0; i < BUFFER_COUNT; i++)
            {
                _InitVertexBase(s_vaos[i]);
                _InitBuffer(s_vaos[i], s_vbos[i]);
            }
        }

        private static void _InitVertexBase(int vao)
        {
            GL.BindVertexArray(vao);
            {
                GL.BindBuffer(BufferTarget.ArrayBuffer, s_vbo_vertexBase);
                {
                    //绑定基本顶点
                    GL.BufferData(BufferTarget.ArrayBuffer, new IntPtr(sizeof(float) * _cacheBaseVertex.Length),
                        _cacheBaseVertex, BufferUsageHint.StaticDraw);

                    GL.EnableVertexAttribArray(1);
                    GL.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, sizeof(float) * (2), 0);
                    GL.VertexAttribDivisor(1, 0);
                }
                GL.BindBuffer(BufferTarget.ArrayBuffer, 0);

                GL.BindBuffer(BufferTarget.ArrayBuffer, s_vbo_texPosBase);
                {
                    //绑定基本顶点
                    GL.BufferData(BufferTarget.ArrayBuffer, new IntPtr(sizeof(float) * _cacheBaseTexPos.Length),
                        _cacheBaseTexPos, BufferUsageHint.StaticDraw);

                    GL.EnableVertexAttribArray(0);
                    GL.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, sizeof(float) * (2), 0);
                    GL.VertexAttribDivisor(0, 0);
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
                    GL.BufferData(BufferTarget.ArrayBuffer, new IntPtr(_VertexSize * _DrawCallInstanceCountMax), IntPtr.Zero, BufferUsageHint.DynamicDraw);
                    
                    //ModelMatrix
                    GL.EnableVertexAttribArray(2);
                    var strip = 0;
                    GL.VertexAttribPointer(2, 2, VertexAttribPointerType.Float, false, _VertexSize, strip);
                    GL.VertexAttribDivisor(2, 1);

                    GL.EnableVertexAttribArray(3);
                    strip += 4 * sizeof(float);
                    GL.VertexAttribPointer(3, 2, VertexAttribPointerType.Float, false, _VertexSize, strip);
                    GL.VertexAttribDivisor(3, 1);

                    GL.EnableVertexAttribArray(4);
                    strip += 4 * sizeof(float);
                    GL.VertexAttribPointer(4, 2, VertexAttribPointerType.Float, false, _VertexSize, strip);
                    GL.VertexAttribDivisor(4, 1);

                    GL.EnableVertexAttribArray(5);
                    strip += 4 * sizeof(float);
                    GL.VertexAttribPointer(5, 2, VertexAttribPointerType.Float, false, _VertexSize, strip);
                    GL.VertexAttribDivisor(5, 1);
                }
                GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
            }
            GL.BindVertexArray(0);
        }

        public void PostRenderCommand(Texture texture, IFumenPreviewer target, Vector2 position, float rotate, Vector2 size, Vector2 anchor)
        {
            /*-----------------CURRENT VERSION------------------ -
			*     modelMatrix
			*     Matrix4x4(16)
			*/

            var modelMatrix =
                    OpenTK.Mathematics.Matrix4.CreateScale(new OpenTK.Mathematics.Vector3(texture.Width, texture.Height, 1)) *
                    OpenTK.Mathematics.Matrix4.CreateScale(new OpenTK.Mathematics.Vector3(size.X / texture.Width, size.Y / texture.Height, 1)) *
                    OpenTK.Mathematics.Matrix4.CreateRotationZ(rotate) *
                    OpenTK.Mathematics.Matrix4.CreateTranslation(position.X - target.ViewWidth / 2, position.Y - target.ViewHeight / 2, 0);

            unsafe
            {
                //Anchor write
                fixed (byte* ptr = &PostData[_currentPostBaseIndex])
                {
                    var copyLen = 4 * 4 * sizeof(float);
                    var basePtr = (byte*)&modelMatrix.Row0.X;
                    for (int i = 0; i < copyLen; i++)
                        ptr[i] = *(basePtr + i);
                }
            }

            CurrentPostCount++;
            _currentPostBaseIndex += _VertexSize;
            if (CurrentPostCount >= Capacity)
            {
                FlushDraw(texture, target);
            }
        }

        private void Draw(IFumenPreviewer target, Texture texture)
        {
            if (CurrentPostCount == 0)
                return;
            s_shader.Begin();

            var VP = target.ViewProjectionMatrix;
            s_shader.PassUniform("ViewProjection", VP);
            s_shader.PassUniform("diffuse", texture);

            GL.BindBuffer(BufferTarget.ArrayBuffer, s_vbos[_current_buffer_index]);
            {
                GL.BufferSubData(BufferTarget.ArrayBuffer, (IntPtr)(0), (IntPtr)(_VertexSize * CurrentPostCount), PostData);
            }
            GL.BindBuffer(BufferTarget.ArrayBuffer, 0);

            GL.BindVertexArray(s_vaos[_current_buffer_index]);
            {
                GL.DrawArraysInstanced(PrimitiveType.TriangleFan, 0, 4, CurrentPostCount);
                performenceMonitor.CountDrawCall(this);
            }
            GL.BindVertexArray(0);
            _current_buffer_index = (_current_buffer_index + 1) % s_vbos.Length;
        }

        public void FlushDraw(Texture texture, IFumenPreviewer target)
        {
            Draw(target, texture);
            Clear();
        }

        private void Clear()
        {
            CurrentPostCount = 0;
            _currentPostBaseIndex = 0;
        }


        public void Dispose()
        {
            GL.DeleteBuffer(s_vbo_vertexBase);
            GL.DeleteBuffer(s_vbo_texPosBase);
            GL.DeleteBuffers(BUFFER_COUNT, s_vbos);
            GL.DeleteVertexArrays(BUFFER_COUNT, s_vaos);
        }

        public void Draw(IFumenPreviewer target, Texture texture, IEnumerable<(Vector2 size, Vector2 position, float rotation)> instances)
        {
            performenceMonitor.OnBeginDrawing(this);
            foreach ((Vector2 size, Vector2 position, float rotation) in instances)
                PostRenderCommand(texture, target, position, rotation, size, new(0.5f, 0.5f));
            FlushDraw(texture, target);
            performenceMonitor.OnAfterDrawing(this);
        }
    }
}
