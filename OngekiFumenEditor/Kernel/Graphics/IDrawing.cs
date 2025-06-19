using OngekiFumenEditor.Modules.FumenVisualEditor.Graphics.Drawing;
using OpenTK.Mathematics;

namespace OngekiFumenEditor.Kernel.Graphics
{
	public interface IDrawing
	{
		void PushOverrideModelMatrix(Matrix4 modelMatrix);
		Matrix4 GetOverrideModelMatrix();
		bool PopOverrideModelMatrix(out Matrix4 modelMatrix);

        void PushOverrideViewMatrix(Matrix4 viewMatrix);
        Matrix4 GetOverrideViewMatrixOrDefault(DrawingTargetContext ctx);
        bool PopOverrideViewMatrix(out Matrix4 viewMatrix);

        void PushOverrideProjectionMatrix(Matrix4 projectionMatrix);
        Matrix4 GetOverrideProjectionMatrixOrDefault(DrawingTargetContext ctx);
        bool PopOverrideProjectionMatrix(out Matrix4 modelMatrix);

        Matrix4 GetOverrideViewProjectMatrixOrDefault(DrawingTargetContext ctx);
	}
}
