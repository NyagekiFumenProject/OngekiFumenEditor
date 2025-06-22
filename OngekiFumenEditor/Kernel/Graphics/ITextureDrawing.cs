using OngekiFumenEditor.Kernel.Graphics;
using System.Collections.Generic;
using System.Numerics;

namespace OngekiFumenEditor.Kernel.Graphics
{
    public interface IHighlightBatchTextureDrawing : IBatchTextureDrawing
    {

    }

    public interface IBatchTextureDrawing : ITextureDrawing
    {
        void Begin(IDrawingContext target, IImage texture);
        void PostSprite(Vector2 size, Vector2 position, float rotation, Vector4 color);
        void End();
    }

    public interface ITextureDrawing : IDrawing
    {
        void Draw(IDrawingContext target, IImage texture, IEnumerable<(Vector2 size, Vector2 position, float rotation, Vector4 color)> instances);
    }
}
