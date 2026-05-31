using System;
using System.Numerics;

namespace OngekiFumenEditor.Kernel.Graphics
{
    /// <summary>
    /// 左手坐标系为世界坐标系的可视区域
    /// </summary>
    public struct VisibleRect : IEquatable<VisibleRect>
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

        public override bool Equals(object obj)
        {
            return obj is VisibleRect rect && Equals(rect);
        }

        public bool Equals(VisibleRect other)
        {
            return TopLeft.Equals(other.TopLeft) &&
                   ButtomRight.Equals(other.ButtomRight);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(TopLeft, ButtomRight);
        }

        public static bool operator ==(VisibleRect left, VisibleRect right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(VisibleRect left, VisibleRect right)
        {
            return !(left == right);
        }
    }
}
