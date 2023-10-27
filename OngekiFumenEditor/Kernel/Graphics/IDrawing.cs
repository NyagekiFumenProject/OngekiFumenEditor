using OpenTK.Mathematics;

namespace OngekiFumenEditor.Kernel.Graphics
{
	public interface IDrawing
	{
		void PushOverrideModelMatrix(Matrix4 modelMatrix);
		Matrix4 GetOverrideModelMatrix();
		bool PopOverrideModelMatrix(out Matrix4 modelMatrix);

		void PushOverrideViewProjectMatrix(Matrix4 viewProjectMatrix);
		Matrix4 GetOverrideViewProjectMatrixOrDefault(IDrawingContext ctx);
		bool PopOverrideViewProjectMatrix(out Matrix4 viewProjectMatrix);
	}
}
