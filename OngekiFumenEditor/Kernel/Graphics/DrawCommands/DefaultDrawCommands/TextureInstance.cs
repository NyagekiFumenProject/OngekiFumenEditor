using System.Numerics;

namespace OngekiFumenEditor.Kernel.Graphics.DrawCommands.DefaultDrawCommands
{
    /// <summary>
    /// Describes one texture sprite instance in a texture draw command.
    /// </summary>
    public readonly record struct TextureInstance
    {
        /// <summary>
        /// Initializes a texture sprite instance.
        /// </summary>
        public TextureInstance(Vector2 size, Vector2 position, float rotation, Vector4 color)
        {
            Size = size;
            Position = position;
            Rotation = rotation;
            Color = color;
        }

        /// <summary>
        /// Gets the sprite size.
        /// </summary>
        public Vector2 Size { get; init; }

        /// <summary>
        /// Gets the sprite position.
        /// </summary>
        public Vector2 Position { get; init; }

        /// <summary>
        /// Gets the sprite rotation.
        /// </summary>
        public float Rotation { get; init; }

        /// <summary>
        /// Gets the sprite tint color.
        /// </summary>
        public Vector4 Color { get; init; }
    }
}
