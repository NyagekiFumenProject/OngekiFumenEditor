using OngekiFumenEditor.Kernel.Graphics.OpenGL;
using OngekiFumenEditor.Kernel.Graphics.OpenGL.Base;
using OngekiFumenEditor.Utils;
using OpenTK.Graphics.OpenGL;
using Polyline2DCSharp;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;

namespace OngekiFumenEditor.Kernel.Graphics.OpenGL.Drawing.LineDrawing
{
    internal sealed class DefaultLineDrawing_old : CommonOpenGLDrawingBase, ILineDrawing, IDisposable
	{
		private readonly CommonLineShader shader;
		private readonly int vbo;
		private readonly int vao;
		private int bufferCapacityInBytes;
        private DefaultOpenGLRenderManagerImpl defaultDrawingManager;

        public DefaultLineDrawing_old(DefaultOpenGLRenderManagerImpl manager) : base(manager)
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
					GL.EnableVertexAttribArray(0);
					GL.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, sizeof(float) * 6, 0);

					GL.EnableVertexAttribArray(1);
					GL.VertexAttribPointer(1, 4, VertexAttribPointerType.Float, false, sizeof(float) * 6, sizeof(float) * 2);
				}
				GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
			}
			GL.BindVertexArray(0);
		}

		public void Draw(IDrawingContext target, IEnumerable<ILineDrawing.LineVertex> points, float lineWidth)
		{
			{
				var count = UpdateBuffer(points, lineWidth);

				shader.Begin();
				{
					shader.PassUniform(shader.ModelLocation, GetOverrideModelMatrix());
					shader.PassUniform(shader.ViewProjectionLocation, GetOverrideViewProjectMatrixOrDefault(target.CurrentDrawingTargetContext));
					GL.BindVertexArray(vao);
					{
						GL.Enable(EnableCap.PolygonSmooth);

						GL.DrawArrays(PrimitiveType.Triangles, 0, count);
						target.RenderContext.PerfomenceMonitor.CountDrawCall();

						GL.Disable(EnableCap.PolygonSmooth);
					}
					GL.BindVertexArray(0);
				}
				shader.End();
			}
		}

		private int UpdateBuffer(IEnumerable<ILineDrawing.LineVertex> points, float lineWidth)
		{
			var vecList = new List<Vec2>();

			var color = default(System.Numerics.Vector4);
			var hasColor = false;

			var inputVecList = new List<Vec2>();
			foreach (var point in points)
			{
				if (!hasColor)
				{
					color = point.Color;
					hasColor = true;
				}

				inputVecList.Add(new Vec2()
				{
					x = point.Point.X,
					y = point.Point.Y
				});
			}

			if (inputVecList.Count == 0)
				return 0;

			var genVertices = Polyline2D.Create(vecList, inputVecList, lineWidth,
				Polyline2D.JointStyle.ROUND,
				Polyline2D.EndCapStyle.ROUND
				);

			var arrBuffer2 = ArrayPool<float>.Shared.Rent(genVertices.Count * 6);
			var arrBufferIdx2 = 0;

			foreach (var p in genVertices)
			{
				arrBuffer2[6 * arrBufferIdx2 + 0] = p.x;
				arrBuffer2[6 * arrBufferIdx2 + 1] = p.y;

				arrBuffer2[6 * arrBufferIdx2 + 2] = color.X;
				arrBuffer2[6 * arrBufferIdx2 + 3] = color.Y;
				arrBuffer2[6 * arrBufferIdx2 + 4] = color.Z;
				arrBuffer2[6 * arrBufferIdx2 + 5] = color.W;

				arrBufferIdx2++;
			}

			var uploadSizeInBytes = sizeof(float) * arrBufferIdx2 * 6;
			if (uploadSizeInBytes > bufferCapacityInBytes)
			{
				bufferCapacityInBytes = uploadSizeInBytes;
				GL.NamedBufferData(vbo, new IntPtr(bufferCapacityInBytes), arrBuffer2, BufferUsageHint.DynamicDraw);
			}
			else
			{
				GL.NamedBufferSubData(vbo, IntPtr.Zero, new IntPtr(uploadSizeInBytes), arrBuffer2);
			}

			ArrayPool<float>.Shared.Return(arrBuffer2);

			return arrBufferIdx2;
		}

		public void Dispose()
		{
			GL.DeleteVertexArray(vao);
			GL.DeleteBuffer(vbo);
		}
	}
}
