using System;
using System.Numerics;

namespace OngekiFumenEditor.Kernel.Graphics.DrawCommands.DefaultDrawCommands
{
    /// <summary>
    /// Represents a string drawing command.
    /// </summary>
    public sealed record DrawStringCommand : DrawCommand
    {
        /// <summary>
        /// Initializes a string drawing command.
        /// </summary>
        public DrawStringCommand(string text, Vector2 position, Vector2 scale, int fontSize, float rotate, Vector4 color, Vector2 origin, IStringDrawing.StringStyle style, IStringDrawing.IFontHandle fontHandle)
        {
            Text = text ?? throw new ArgumentNullException(nameof(text));
            Position = position;
            Scale = scale;
            FontSize = fontSize;
            Rotate = rotate;
            Color = color;
            Origin = origin;
            Style = style;
            FontHandle = fontHandle ?? throw new ArgumentNullException(nameof(fontHandle));
        }

        /// <summary>
        /// Gets the text to draw.
        /// </summary>
        public string Text { get; init; }

        /// <summary>
        /// Gets the text position.
        /// </summary>
        public Vector2 Position { get; init; }

        /// <summary>
        /// Gets the text scale.
        /// </summary>
        public Vector2 Scale { get; init; }

        /// <summary>
        /// Gets the font size.
        /// </summary>
        public int FontSize { get; init; }

        /// <summary>
        /// Gets the text rotation.
        /// </summary>
        public float Rotate { get; init; }

        /// <summary>
        /// Gets the text tint color.
        /// </summary>
        public Vector4 Color { get; init; }

        /// <summary>
        /// Gets the text origin.
        /// </summary>
        public Vector2 Origin { get; init; }

        /// <summary>
        /// Gets the requested string style flags.
        /// </summary>
        public IStringDrawing.StringStyle Style { get; init; }

        /// <summary>
        /// Gets the font handle used by the command.
        /// </summary>
        public IStringDrawing.IFontHandle FontHandle { get; init; }
    }
}
