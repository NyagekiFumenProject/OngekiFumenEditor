using OngekiFumenEditor.Utils.ObjectPool;
using System;
using System.Collections.Generic;

namespace OngekiFumenEditor.Kernel.Graphics.DrawCommands.DefaultDrawCommands
{
    /// <summary>
    /// Represents a highlighted batched texture drawing command.
    /// </summary>
    public sealed class DrawHighlightBatchTextureCommand : DrawCommand
    {
        private IPooledList<TextureInstance> instances;

        /// <summary>
        /// Initializes a new instance of the <see cref="DrawHighlightBatchTextureCommand"/> class.
        /// </summary>
        public DrawHighlightBatchTextureCommand()
        {
        }

        internal DrawHighlightBatchTextureCommand Initialize(IImage texture, IPooledList<TextureInstance> instances)
        {
            Texture = texture ?? throw new ArgumentNullException(nameof(texture));
            this.instances = instances ?? throw new ArgumentNullException(nameof(instances));
            return this;
        }

        /// <summary>
        /// Gets the texture resource to draw.
        /// </summary>
        public IImage Texture { get; private set; }

        /// <summary>
        /// Gets the sprite instances owned by this command.
        /// </summary>
        public IReadOnlyList<TextureInstance> Instances => instances is null ? Array.Empty<TextureInstance>() : instances;

        /// <summary>
        /// Releases the pooled sprite list owned by this command.
        /// </summary>
        public override void Dispose()
        {
            instances?.Dispose();
            instances = null;
            Texture = null;
        }

        /// <inheritdoc />
        protected override void ReturnToPoolCore()
        {
            ObjectPool<DrawHighlightBatchTextureCommand>.Return(this);
        }
    }
}
