using System.Numerics;

namespace OngekiFumenEditor.Kernel.Graphics
{
    public interface ICircleDrawing : IDrawing
    {
        void Begin(IDrawingContext target);
        void Post(Vector2 point, Vector4 color, bool isSolid, float radius, float hollowLineWidth);
        void Post(Vector2 point, Vector4 color, bool isSolid, float radius) => Post(point, color, isSolid, radius, 0);
        void End();
    }
}
