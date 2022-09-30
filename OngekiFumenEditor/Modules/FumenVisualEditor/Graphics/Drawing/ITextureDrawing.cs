using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Graphics.Drawing
{
    public interface IHighlightBatchTextureDrawing : ITextureDrawing
    {
        void Begin(IFumenEditorDrawingContext target, Texture texture);
        void PostSprite(Vector2 size, Vector2 position, float rotation);
        void End();
    }

    public interface IBatchTextureDrawing : ITextureDrawing
    {
        void Begin(IFumenEditorDrawingContext target, Texture texture);
        void PostSprite(Vector2 size, Vector2 position, float rotation);
        void End();
    }

    public interface ITextureDrawing : IDrawing
    {
        void Draw(IFumenEditorDrawingContext target, Texture texture, IEnumerable<(Vector2 size, Vector2 position, float rotation)> instances);
    }
}
