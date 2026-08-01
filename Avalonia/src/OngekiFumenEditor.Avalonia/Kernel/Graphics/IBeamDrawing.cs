using OpenTK.Mathematics;

namespace OngekiFumenEditor.Avalonia.Kernel.Graphics
{
	public interface IBeamDrawing : IDrawing
	{
		void Draw(IDrawingContext target, IImage tex, int width, float x, float progress, Vector4 color, float rotate, float judgeOffset);
	}
}


