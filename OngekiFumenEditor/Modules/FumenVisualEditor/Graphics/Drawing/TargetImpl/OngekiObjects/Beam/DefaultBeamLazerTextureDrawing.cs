using OngekiFumenEditor.Kernel.Graphics;
using OngekiFumenEditor.Kernel.Graphics.Base;
using OngekiFumenEditor.Kernel.Graphics.Drawing.DefaultDrawingImpl;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using System;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Graphics.Drawing.TargetImpl.OngekiObjects.Beam
{
	public class DefaultBeamLazerTextureDrawing : CommonDrawingBase, IDisposable
	{
		private readonly Shader shader;
		private readonly int vertexVBO;
		private readonly int textureVBO;
		private readonly int vao;

		public DefaultBeamLazerTextureDrawing()
		{
			shader = new BeamLazerShader();
			shader.Compile();

			vertexVBO = GL.GenBuffer();
			textureVBO = GL.GenBuffer();

			vao = GL.GenVertexArray();

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

		public void Dispose()
		{
			GL.DeleteVertexArray(vao);
			GL.DeleteBuffer(vertexVBO);
			GL.DeleteBuffer(textureVBO);
		}

		public void Draw(IDrawingContext target, Texture texture, int width, float x, float progress, Vector4 color, float rotate, float judgeOffset)
		{
			target.PerfomenceMonitor.OnBeginDrawing(this);
			{
				var textureScaleY = target.ViewHeight / texture.Height;

				var modelMatrix =
					GetOverrideModelMatrix() *
					(Matrix4.CreateScale(new Vector3(texture.Width, texture.Height, 1))
					* Matrix4.CreateScale(new Vector3(width * 1.0f / texture.Width, target.ViewHeight * 2.0f / texture.Height, 1))) *
					Matrix4.CreateRotationZ(rotate) *
					Matrix4.CreateTranslation(x, target.ViewHeight / 2 + target.Rect.MinY + judgeOffset / 2, 0);

				shader.Begin();
				{
					shader.PassUniform("Model", modelMatrix);
					shader.PassUniform("ViewProjection", GetOverrideViewProjectMatrixOrDefault(target));
					shader.PassUniform("textureScaleY", textureScaleY);
					shader.PassUniform("diffuse", texture);
					shader.PassUniform("color", color);
					shader.PassUniform("progress", progress);

					GL.BindVertexArray(vao);
					{
						GL.DrawArrays(PrimitiveType.TriangleFan, 0, 4);
						target.PerfomenceMonitor.CountDrawCall(this);
					}
					GL.BindVertexArray(0);
				}
				shader.End();
			}
			target.PerfomenceMonitor.OnAfterDrawing(this);
		}
	}
}
