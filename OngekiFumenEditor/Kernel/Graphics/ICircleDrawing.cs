using System.Numerics;

namespace OngekiFumenEditor.Kernel.Graphics
{
	public interface ICircleDrawing : IDrawing
	{
		void Begin(IDrawingContext target);
		void Post(Vector2 point, Vector4 color, bool isSolid, float radius);
		void End();
	}
}
