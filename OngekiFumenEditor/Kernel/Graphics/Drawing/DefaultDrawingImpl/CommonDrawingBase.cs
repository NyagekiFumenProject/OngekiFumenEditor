using OpenTK.Mathematics;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace OngekiFumenEditor.Kernel.Graphics.Drawing.DefaultDrawingImpl
{
	public class CommonDrawingBase : IDrawing
	{
		private Stack<Matrix4> modelMatrices = new Stack<Matrix4>(new[] { Matrix4.Identity });
		private Stack<Matrix4> viewProjectMatrices = new Stack<Matrix4>();

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void PushOverrideModelMatrix(Matrix4 modelMatrix)
		{
			modelMatrices.Push(modelMatrix);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool PopOverrideModelMatrix(out Matrix4 modelMatrix)
		{
			modelMatrix = Matrix4.Identity;
			if (modelMatrices.Count <= 1)
				return false;

			modelMatrix = modelMatrices.Pop();
			return true;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Matrix4 GetOverrideModelMatrix()
		{
			return modelMatrices.Peek();
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void PushOverrideViewProjectMatrix(Matrix4 viewProjectMatrix)
		{
			viewProjectMatrices.Push(viewProjectMatrix);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Matrix4 GetOverrideViewProjectMatrixOrDefault(IDrawingContext ctx)
		{
			return viewProjectMatrices.Count == 0 ? ctx.ViewProjectionMatrix : viewProjectMatrices.Peek();
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool PopOverrideViewProjectMatrix(out Matrix4 viewProjectMatrix)
		{
			viewProjectMatrix = Matrix4.Identity;
			if (viewProjectMatrices.Count == 0)
				return false;

			viewProjectMatrix = viewProjectMatrices.Pop();
			return true;
		}
	}
}
