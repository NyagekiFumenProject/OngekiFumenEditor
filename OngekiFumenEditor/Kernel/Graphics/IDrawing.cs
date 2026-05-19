using OngekiFumenEditor.Modules.FumenVisualEditor.Graphics.Drawing;
using System.Numerics;

namespace OngekiFumenEditor.Kernel.Graphics
{
	public interface IDrawing
	{
		void PushOverrideModelMatrix(Matrix4x4 modelMatrix);
		Matrix4x4 GetOverrideModelMatrix();
		bool PopOverrideModelMatrix(out Matrix4x4 modelMatrix);

        void PushOverrideViewMatrix(Matrix4x4 viewMatrix);
        Matrix4x4 GetOverrideViewMatrixOrDefault(DrawingTargetContext ctx);
        bool PopOverrideViewMatrix(out Matrix4x4 viewMatrix);

        void PushOverrideProjectionMatrix(Matrix4x4 projectionMatrix);
        Matrix4x4 GetOverrideProjectionMatrixOrDefault(DrawingTargetContext ctx);
        bool PopOverrideProjectionMatrix(out Matrix4x4 modelMatrix);

        Matrix4x4 GetOverrideViewProjectMatrixOrDefault(DrawingTargetContext ctx);
	}
}
