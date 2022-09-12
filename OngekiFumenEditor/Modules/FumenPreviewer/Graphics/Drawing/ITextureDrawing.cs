using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Modules.FumenPreviewer.Graphics.Drawing
{
    public interface IBatchTextureDrawing : ITextureDrawing
    {

    }

    public interface ITextureDrawing : IDrawing
    {
        void Draw(IFumenPreviewer target, Texture texture, IEnumerable<(Vector2 size, Vector2 position, float rotation)> instances);
    }
}
