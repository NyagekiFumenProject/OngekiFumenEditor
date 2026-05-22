using System;
using System.Numerics;

namespace OngekiFumenEditor.Kernel.Graphics.DrawCommands.DefaultDrawCommands
{
    /// <summary>
    /// Represents a beam drawing command.
    /// </summary>
    public sealed record DrawBeamCommand : DrawCommand
    {
        /// <summary>
        /// Initializes a beam drawing command.
        /// </summary>
        public DrawBeamCommand(IImage texture, int width, float x, float progress, Vector4 color, float rotate, float judgeOffset)
        {
            Texture = texture ?? throw new ArgumentNullException(nameof(texture));
            Width = width;
            X = x;
            Progress = progress;
            Color = color;
            Rotate = rotate;
            JudgeOffset = judgeOffset;
        }

        /// <summary>
        /// Gets the beam texture.
        /// </summary>
        public IImage Texture { get; init; }

        /// <summary>
        /// Gets the beam width.
        /// </summary>
        public int Width { get; init; }

        /// <summary>
        /// Gets the beam X position.
        /// </summary>
        public float X { get; init; }

        /// <summary>
        /// Gets the beam progress.
        /// </summary>
        public float Progress { get; init; }

        /// <summary>
        /// Gets the beam tint color.
        /// </summary>
        public Vector4 Color { get; init; }

        /// <summary>
        /// Gets the beam rotation.
        /// </summary>
        public float Rotate { get; init; }

        /// <summary>
        /// Gets the judge offset.
        /// </summary>
        public float JudgeOffset { get; init; }
    }
}
