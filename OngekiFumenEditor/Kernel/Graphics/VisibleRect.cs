using OpenTK.Mathematics;

namespace OngekiFumenEditor.Kernel.Graphics
{
    /// <summary>
    /// 左手坐标系为世界坐标系的可视区域
    /// </summary>
    public struct VisibleRect
    {
        public VisibleRect(Vector2 buttomRight, Vector2 topLeft)
        {
            TopLeft = topLeft;
            ButtomRight = buttomRight;
        }

        public Vector2 TopLeft { get; init; }
        public Vector2 ButtomRight { get; init; }

        public float Width => ButtomRight.X - TopLeft.X;
        public float Height => TopLeft.Y - ButtomRight.Y;

        public float MinY => ButtomRight.Y;
        public float MaxY => TopLeft.Y;

        public float CenterX => (ButtomRight.X + TopLeft.X) / 2;
        public float CenterY => (ButtomRight.Y + TopLeft.Y) / 2;

        public float MinX => TopLeft.X;
        public float MaxX => ButtomRight.X;
    }
}
