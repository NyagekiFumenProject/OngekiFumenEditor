using System.Collections.Generic;
using System.Numerics;

namespace OngekiFumenEditor.Kernel.Graphics
{
	public interface ISimpleLineDrawing : ILineDrawing, IStaticVBODrawing
	{
		void Begin(IDrawingContext target, float lineWidth);
		void PostPoint(Vector2 Point, Vector4 Color, VertexDash dash);
		void End();

		IVBOHandle GenerateVBOWithPresetPoints(IEnumerable<LineVertex> points, float lineWidth);
	}
}