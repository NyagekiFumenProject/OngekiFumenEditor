using System.Numerics;
using OngekiFumenEditor.Avalonia.Base.EditorObjects.Svg;

namespace OngekiFumenEditor.Avalonia.Kernel.Graphics;

public interface ISvgDrawing : IDrawing
{
    void Draw(IDrawingContext target, SvgPrefabBase svg, Vector2 position);
}
