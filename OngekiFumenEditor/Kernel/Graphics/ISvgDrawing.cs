using OngekiFumenEditor.Base.EditorObjects.Svg;
using System.Numerics;

namespace OngekiFumenEditor.Kernel.Graphics
{
	public interface ISvgDrawing : IDrawing
	{
		void Draw(IDrawingContext target, SvgPrefabBase svg, Vector2 position);
	}
}
