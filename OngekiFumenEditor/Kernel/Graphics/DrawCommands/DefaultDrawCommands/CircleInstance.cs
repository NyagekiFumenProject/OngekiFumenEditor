using System.Numerics;

namespace OngekiFumenEditor.Kernel.Graphics.DrawCommands.DefaultDrawCommands
{
    /// <summary>
    /// Describes one circle instance in a circle draw command.
    /// </summary>
    public readonly record struct CircleInstance
    {
        /// <summary>
        /// Initializes a circle instance.
        /// </summary>
        public CircleInstance(Vector2 point, Vector4 color, bool isSolid, float radius, float hollowLineWidth)
        {
            Point = point;
            Color = color;
            IsSolid = isSolid;
            Radius = radius;
            HollowLineWidth = hollowLineWidth;
        }

        /// <summary>
        /// Gets the circle center point.
        /// </summary>
        public Vector2 Point { get; init; }

        /// <summary>
        /// Gets the circle color.
        /// </summary>
        public Vector4 Color { get; init; }

        /// <summary>
        /// Gets whether the circle should be filled.
        /// </summary>
        public bool IsSolid { get; init; }

        /// <summary>
        /// Gets the circle radius.
        /// </summary>
        public float Radius { get; init; }

        /// <summary>
        /// Gets the line width used when drawing a hollow circle.
        /// </summary>
        public float HollowLineWidth { get; init; }
    }
}
