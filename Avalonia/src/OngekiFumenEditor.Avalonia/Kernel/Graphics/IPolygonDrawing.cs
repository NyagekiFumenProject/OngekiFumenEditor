using System.Numerics;

namespace OngekiFumenEditor.Avalonia.Kernel.Graphics
{
	public interface IPolygonDrawing : IDrawing
	{
		void Begin(IDrawingContext target, Primitive primitive);
		void PostPoint(Vector2 Point, Vector4 Color);
		void End();
	}
}


