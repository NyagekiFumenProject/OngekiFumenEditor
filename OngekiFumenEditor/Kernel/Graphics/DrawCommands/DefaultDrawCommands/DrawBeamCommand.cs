using OngekiFumenEditor.Utils.ObjectPool;
using System;
using System.Numerics;

namespace OngekiFumenEditor.Kernel.Graphics.DrawCommands.DefaultDrawCommands
{
    /// <summary>
    /// Represents a beam drawing command.
    /// </summary>
    public sealed class DrawBeamCommand : DrawCommand
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DrawBeamCommand"/> class.
        /// </summary>
        public DrawBeamCommand()
        {
        }

        internal DrawBeamCommand Initialize(IImage texture, int width, float x, float progress, Vector4 color, float rotate, float judgeOffset)
        {
            Texture = texture ?? throw new ArgumentNullException(nameof(texture));
            Width = width;
            X = x;
            Progress = progress;
            Color = color;
            Rotate = rotate;
            JudgeOffset = judgeOffset;
            return this;
        }

        /// <summary>
        /// Gets the beam texture.
        /// </summary>
        public IImage Texture { get; private set; }

        /// <summary>
        /// Gets the beam width.
        /// </summary>
        public int Width { get; private set; }

        /// <summary>
        /// Gets the beam X position.
        /// </summary>
        public float X { get; private set; }

        /// <summary>
        /// Gets the beam progress.
        /// </summary>
        public float Progress { get; private set; }

        /// <summary>
        /// Gets the beam tint color.
        /// </summary>
        public Vector4 Color { get; private set; }

        /// <summary>
        /// Gets the beam rotation.
        /// </summary>
        public float Rotate { get; private set; }

        /// <summary>
        /// Gets the judge offset.
        /// </summary>
        public float JudgeOffset { get; private set; }

        /// <inheritdoc />
        public override void Dispose()
        {
            Texture = null;
            Width = default;
            X = default;
            Progress = default;
            Color = default;
            Rotate = default;
            JudgeOffset = default;
        }

        /// <inheritdoc />
        protected override void ReturnToPoolCore()
        {
            ObjectPool<DrawBeamCommand>.Return(this);
        }
    }
}
