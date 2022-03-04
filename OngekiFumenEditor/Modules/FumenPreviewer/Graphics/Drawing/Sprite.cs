using Caliburn.Micro;
using OngekiFumenEditor.Modules.FumenPreviewer.Graphics.Drawing.Shaders;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Modules.FumenPreviewer.Graphics.Drawing
{
    public class Sprite : IDisposable
    {
        private readonly Texture texture;
        private readonly Shader shader;
        private readonly int vertexVBO;
        private readonly int textureVBO;
        private readonly int vao;

        public Vector2 Size { get; set; } = new Vector2(40, 40);
        public Vector2 Scale { get; set; } = new Vector2(1, 1);
        public Vector2 Position { get; set; } = new Vector2(1, 1);
        public float Rotate { get; set; } = 0;

        public Sprite(Texture texture)
        {
            this.texture = texture;
            Size = new(texture.Width, texture.Height);

            shader = CommonSpriteShader.Shared;

            vertexVBO = GL.GenBuffer();
            textureVBO = GL.GenBuffer();

            vao = GL.GenVertexArray();

            InitVAO();
        }

        private void InitVAO()
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
                            -0.5f * texture.Width, 0.5f * texture.Height,
                             0.5f * texture.Width, 0.5f * texture.Height,
                             0.5f * texture.Width, -0.5f * texture.Height,
                            -0.5f * texture.Width, -0.5f * texture.Height,
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

        public void Draw(Matrix4 viewProjectionMatrix)
        {
            var modelMatrix =  Matrix4.CreateRotationY(Rotate) * Matrix4.CreateScale(Scale.Y, Scale.Y, 1) * Matrix4.CreateTranslation(Position.X, Position.Y, 0);

            shader.Begin();
            shader.PassUniform("Model", modelMatrix);
            shader.PassUniform("ViewProjection", viewProjectionMatrix);
            shader.PassUniform("TextureSize", Size);
            shader.PassUniform("diffuse", texture);

            GL.BindVertexArray(vao);
            {
                GL.DrawArrays(PrimitiveType.TriangleFan, 0, 4);
            }
            GL.BindVertexArray(0);

            shader.End();
        }

        public void Dispose()
        {
            GL.DeleteVertexArray(vao);
            GL.DeleteBuffer(vertexVBO);
            GL.DeleteBuffer(textureVBO);
        }
    }
}
