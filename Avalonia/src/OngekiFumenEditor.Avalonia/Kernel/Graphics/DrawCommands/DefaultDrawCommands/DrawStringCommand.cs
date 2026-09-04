using OngekiFumenEditor.Avalonia.Utils.ObjectPool;
using System;
using System.Numerics;

namespace OngekiFumenEditor.Avalonia.Kernel.Graphics.DrawCommands.DefaultDrawCommands
{
    /// <summary>
    /// Represents a string drawing command.
    /// </summary>
    public sealed class DrawStringCommand : DrawCommand
    {
        public DrawStringCommand()
        {
        }

        internal DrawStringCommand Initialize(string text, Vector2 position, Vector2 scale, int fontSize, float rotate, Vector4 color, Vector2 origin, IStringDrawing.StringStyle style, IStringDrawing.IFontHandle fontHandle)
        {
            Text = text ?? throw new ArgumentNullException(nameof(text));
            Position = position;
            Scale = scale;
            FontSize = fontSize;
            Rotate = rotate;
            Color = color;
            Origin = origin;
            Style = style;
            FontHandle = fontHandle;
            return this;
        }

        /// <summary>
        /// Gets the text to draw.
        /// </summary>
        public string Text { get; private set; }

        /// <summary>
        /// Gets the text position.
        /// </summary>
        public Vector2 Position { get; private set; }

        /// <summary>
        /// Gets the text scale.
        /// </summary>
        public Vector2 Scale { get; private set; }

        /// <summary>
        /// Gets the font size.
        /// </summary>
        public int FontSize { get; private set; }

        /// <summary>
        /// Gets the text rotation.
        /// </summary>
        public float Rotate { get; private set; }

        /// <summary>
        /// Gets the text tint color.
        /// </summary>
        public Vector4 Color { get; private set; }

        /// <summary>
        /// Gets the text origin.
        /// </summary>
        public Vector2 Origin { get; private set; }

        /// <summary>
        /// Gets the requested string style flags.
        /// </summary>
        public IStringDrawing.StringStyle Style { get; private set; }

        /// <summary>
        /// Gets the font handle used by the command. Null requests the backend default font.
        /// </summary>
        public IStringDrawing.IFontHandle FontHandle { get; private set; }

        public override void Dispose()
        {
            Text = null;
            Position = default;
            Scale = default;
            FontSize = default;
            Rotate = default;
            Color = default;
            Origin = default;
            Style = default;
            FontHandle = null;
        }

        protected override void ReturnToPoolCore()
        {
            ObjectPool<DrawStringCommand>.Return(this);
        }
    }
}