using OngekiFumenEditor.Modules.FumenVisualEditor.Graphics.Drawing;
using OpenTK.Mathematics;

namespace OngekiFumenEditor.Kernel.Graphics
{
	public interface IDrawing
	{
		void PushOverrideModelMatrix(Matrix4 modelMatrix);
		Matrix4 GetOverrideModelMatrix();
		bool PopOverrideModelMatrix(out Matrix4 modelMatrix);

		void PushOverrideViewProjectMatrix(Matrix4 viewProjectMatrix);
		Matrix4 GetOverrideViewProjectMatrixOrDefault(DrawingTargetContext ctx);
		bool PopOverrideViewProjectMatrix(out Matrix4 viewProjectMatrix);
	}
}
