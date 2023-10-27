using OngekiFumenEditor.Kernel.Graphics.Base;
using System.Collections.Generic;
using System.Numerics;

namespace OngekiFumenEditor.Kernel.Graphics
{
	public interface IHighlightBatchTextureDrawing : ITextureDrawing
	{
		void Begin(IDrawingContext target, Texture texture);
		void PostSprite(Vector2 size, Vector2 position, float rotation);
		void End();
	}

	public interface IBatchTextureDrawing : ITextureDrawing
	{
		void Begin(IDrawingContext target, Texture texture);
		void PostSprite(Vector2 size, Vector2 position, float rotation);
		void End();
	}

	public interface ITextureDrawing : IDrawing
	{
		void Draw(IDrawingContext target, Texture texture, IEnumerable<(Vector2 size, Vector2 position, float rotation)> instances);
	}
}
