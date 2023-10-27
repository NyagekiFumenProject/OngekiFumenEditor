using OpenTK.Graphics.OpenGL;
using System;
using System.ComponentModel.Composition;
using System.Numerics;

namespace OngekiFumenEditor.Kernel.Graphics.Drawing.DefaultDrawingImpl.CircleDrawing
{
	[Export(typeof(ICircleDrawing))]
	[PartCreationPolicy(CreationPolicy.Shared)]
	internal class DefaultInstancedCircleDrawing : CommonDrawingBase, ICircleDrawing
	{
		private Base.Shader shader;
		private float[] postData;
		private int currentPostBaseIndex = 0;
		private int currentPostCount = 0;
		private int vao;
		private int vbo;
		private IDrawingContext target;
		//private float backupPointSize;
		private const int VertexSize = (4 + 2 + 1) * sizeof(float);
		private const int MAX_DRAW_COUNT = 3000;
		private const float MAX_CIRCILE_SIZE = 50f;

		public DefaultInstancedCircleDrawing()
		{
			shader = BatchCircleShader.Shared;

			postData = new float[VertexSize / sizeof(float) * MAX_DRAW_COUNT];

			vao = GL.GenVertexArray();
			vbo = GL.GenBuffer();

			InitBuffer(vao, vbo);

			GL.PointSize(MAX_CIRCILE_SIZE);
		}

		private void InitBuffer(int vao, int vbo)
		{
			GL.BindVertexArray(vao);
			{
				GL.BindBuffer(BufferTarget.ArrayBuffer, vbo);
				{
					GL.BufferData(BufferTarget.ArrayBuffer, new IntPtr(VertexSize * MAX_DRAW_COUNT), IntPtr.Zero, BufferUsageHint.DynamicDraw);

					//setup vertex struct

					//color
					GL.EnableVertexAttribArray(0);
					var strip = 0;
					GL.VertexAttribPointer(0, 4, VertexAttribPointerType.Float, false, VertexSize, strip);
					//GL.VertexAttribDivisor(0, 1);

					//position
					GL.EnableVertexAttribArray(1);
					strip += 4 * sizeof(float);
					GL.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, VertexSize, strip);
					//GL.VertexAttribDivisor(1, 1);

					//radius
					GL.EnableVertexAttribArray(2);
					strip += 2 * sizeof(float);
					GL.VertexAttribPointer(2, 1, VertexAttribPointerType.Float, false, VertexSize, strip);
					//GL.VertexAttribDivisor(2, 1);
				}
				GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
			}
			GL.BindVertexArray(0);
		}

		public void Begin(IDrawingContext target)
		{
			target.PerfomenceMonitor.OnBeginDrawing(this);
			this.target = target;

			var viewWidth = target.ViewWidth;
			var viewHeight = target.ViewHeight;

			shader.Begin();
			GL.BindVertexArray(vao);

			shader.PassUniform("uResolution", new Vector2(viewWidth, viewHeight));
			var mvpMatrix = GetOverrideModelMatrix() * GetOverrideViewProjectMatrixOrDefault(target);
			shader.PassUniform("ModelViewProjection", mvpMatrix);

			//backupPointSize = GL.GetFloat(GetPName.PointSize);
		}

		private void FlushDraw()
		{
			Draw();
			Clear();
		}

		private void Draw()
		{
			if (currentPostCount == 0)
				return;

			GL.NamedBufferSubData(vbo, (IntPtr)0, (IntPtr)(VertexSize * currentPostCount), postData);

			GL.DrawArrays(PrimitiveType.Points, 0, currentPostCount);
			target.PerfomenceMonitor.CountDrawCall(this);
		}

		private void Clear()
		{
			currentPostCount = 0;
			currentPostBaseIndex = 0;
		}

		public void End()
		{
			FlushDraw();
			GL.BindVertexArray(0);
			shader.End();
			target.PerfomenceMonitor.OnAfterDrawing(this);
			target = default;

			//GL.PointSize(backupPointSize);
		}

		public void Post(Vector2 point, Vector4 color, bool isSolid, float radius)
		{

			/*-----------------CURRENT VERSION------------------ -
			*     color          position             radius
			*     vec4 (16)       vec2 (8)           float(4)  
			*/


			var buffer = postData.AsSpan().Slice(currentPostBaseIndex / sizeof(float), VertexSize / sizeof(float));

			buffer[0] = color.X;
			buffer[1] = color.Y;
			buffer[2] = color.Z;
			buffer[3] = color.W;

			buffer[4] = point.X;
			buffer[5] = point.Y;

			buffer[6] = radius;

			currentPostCount++;
			currentPostBaseIndex += VertexSize;
			if (currentPostCount >= MAX_DRAW_COUNT)
				FlushDraw();
		}
	}
}
