using System;

namespace OngekiFumenEditor.Kernel.Graphics.DrawCommands.DefaultDrawCommands
{
    /// <summary>
    /// Represents a static VBO drawing command.
    /// </summary>
    public sealed record DrawStaticVBOCommand : DrawCommand
    {
        /// <summary>
        /// Initializes a static VBO drawing command.
        /// </summary>
        public DrawStaticVBOCommand(IStaticVBODrawing.IVBOHandle vbo)
        {
            VBO = vbo ?? throw new ArgumentNullException(nameof(vbo));
        }

        /// <summary>
        /// Gets the VBO handle to draw.
        /// </summary>
        public IStaticVBODrawing.IVBOHandle VBO { get; init; }
    }
}
