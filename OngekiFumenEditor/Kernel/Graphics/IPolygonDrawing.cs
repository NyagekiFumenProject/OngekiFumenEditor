using OpenTK.Graphics.OpenGL;
using System.Numerics;

namespace OngekiFumenEditor.Kernel.Graphics
{
	public interface IPolygonDrawing : IDrawing
	{
		int AvailablePostableVertexCount { get; }
		void Begin(IDrawingContext target, PrimitiveType primitive);
		void PostPoint(Vector2 Point, Vector4 Color);
		void End();
	}
}
