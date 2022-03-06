using Caliburn.Micro;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Modules.FumenPreviewer.Graphics.Drawing.Shaders;
using OngekiFumenEditor.Modules.FumenPreviewer.Graphics.PrimitiveValue;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Modules.FumenPreviewer.Graphics.Drawing
{
    public abstract class CommonSpriteDrawTargetBase<T> : CommonDrawTargetBase<T>, IDisposable where T : OngekiObjectBase
    {
        private readonly Shader shader;
        private readonly int vertexVBO;
        private readonly int textureVBO;
        private readonly int vao;
        private readonly IFumenPreviewer previewer;

        public IFumenPreviewer Previewer => previewer;

        protected CommonSpriteDrawTargetBase()
        {
            shader = CommonSpriteShader.Shared;

            vertexVBO = GL.GenBuffer();
            textureVBO = GL.GenBuffer();

            vao = GL.GenVertexArray();

            previewer = IoC.Get<IFumenPreviewer>();

            Init();
        }

        private void Init()
        {
            GL.BindVertexArray(vao);
            {
                GL.BindBuffer(BufferTarget.ArrayBuffer, textureVBO);
                {
                    //绑定基本顶点
                    var _cacheBaseTexPos = new float[] {
                             0,0,
                             1,0,
                             1,1,
                             0,1
                    };
                    GL.BufferData(BufferTarget.ArrayBuffer, new IntPtr(sizeof(float) * _cacheBaseTexPos.Length),
                        _cacheBaseTexPos, BufferUsageHint.StaticDraw);

                    GL.EnableVertexAttribArray(0);
                    GL.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, sizeof(float) * 2, 0);
                }
                GL.BindBuffer(BufferTarget.ArrayBuffer, 0);

                GL.BindBuffer(BufferTarget.ArrayBuffer, vertexVBO);
                {
                    //绑定基本顶点
                    var cacheBaseVertex = new float[] {
                            -0.5f , 0.5f ,
                             0.5f , 0.5f ,
                             0.5f , -0.5f ,
                            -0.5f , -0.5f ,
                    };
                    GL.BufferData(BufferTarget.ArrayBuffer, new IntPtr(sizeof(float) * cacheBaseVertex.Length),
                        cacheBaseVertex, BufferUsageHint.StaticDraw);

                    GL.EnableVertexAttribArray(1);
                    GL.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, sizeof(float) * 2, 0);
                }
                GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
            }
            GL.BindVertexArray(0);
        }

        public void Draw(Texture texture, Vector2 size, Vector position, float rotation)
        {
            var modelMatrix =
                Matrix4.CreateRotationY(rotation) *
                Matrix4.CreateScale(new Vector3(texture.Width, texture.Height, 1)) *
                Matrix4.CreateScale(new Vector3(size.X / texture.Width, size.Y / texture.Height, 1)) *
                Matrix4.CreateTranslation(position.X - previewer.ViewWidth / 2, position.Y - previewer.ViewHeight / 2, 0);
            //var mvpMatrix = previewer.ViewProjectionMatrix * modelMatrix;

            shader.Begin();
            shader.PassUniform("Model", modelMatrix);
            shader.PassUniform("ViewProjection", previewer.ViewProjectionMatrix);
            shader.PassUniform("TextureSize", size);
            shader.PassUniform("diffuse", texture);

            GL.BindVertexArray(vao);
            {
                GL.DrawArrays(PrimitiveType.TriangleFan, 0, 4);
            }
            GL.BindVertexArray(0);

            shader.End();
        }

        public virtual void Dispose()
        {
            GL.DeleteVertexArray(vao);
            GL.DeleteBuffer(vertexVBO);
            GL.DeleteBuffer(textureVBO);
        }
    }
}
