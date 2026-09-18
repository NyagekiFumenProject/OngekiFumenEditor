using OngekiFumenEditor.Utils.ObjectPool;
using System;
using System.Collections.Generic;

namespace OngekiFumenEditor.Kernel.Graphics.DrawCommands.DefaultDrawCommands
{
    /// <summary>
    /// Represents a batched texture drawing command.
    /// </summary>
    public sealed class DrawBatchTextureCommand : DrawCommand
    {
        private IPooledList<TextureInstance> instances;

        /// <summary>
        /// Initializes a new instance of the <see cref="DrawBatchTextureCommand"/> class.
        /// </summary>
        public DrawBatchTextureCommand()
        {
        }

        internal DrawBatchTextureCommand Initialize(IImage texture, IPooledList<TextureInstance> instances)
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
            ObjectPool<DrawBatchTextureCommand>.Return(this);
        }
    }
}
