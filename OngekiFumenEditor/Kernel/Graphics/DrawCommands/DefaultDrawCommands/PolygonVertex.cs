using System.Numerics;

namespace OngekiFumenEditor.Kernel.Graphics.DrawCommands.DefaultDrawCommands
{
    /// <summary>
    /// Describes one polygon vertex in a polygon draw command.
    /// </summary>
    public readonly record struct PolygonVertex
    {
        /// <summary>
        /// Initializes a polygon vertex.
        /// </summary>
        public PolygonVertex(Vector2 point, Vector4 color)
        {
            Point = point;
            Color = color;
        }

        /// <summary>
        /// Gets the vertex position.
        /// </summary>
        public Vector2 Point { get; init; }

        /// <summary>
        /// Gets the vertex color.
        /// </summary>
        public Vector4 Color { get; init; }
    }
}
