using OngekiFumenEditor.Core.Utils.ObjectPool;
using System;
using System.Collections.Generic;

namespace OngekiFumenEditor.Kernel.Graphics.DrawCommands.DefaultDrawCommands
{
    /// <summary>
    /// Represents a texture drawing command.
    /// </summary>
    public sealed record DrawTextureCommand : DrawCommand
    {
        private IPooledList<TextureInstance> instances;

        /// <summary>
        /// Initializes a texture drawing command.
        /// </summary>
        public DrawTextureCommand(IImage texture, IPooledList<TextureInstance> instances)
        {
            Texture = texture ?? throw new ArgumentNullException(nameof(texture));
            this.instances = instances ?? throw new ArgumentNullException(nameof(instances));
        }

        /// <summary>
        /// Gets the texture resource to draw.
        /// </summary>
        public IImage Texture { get; }

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
        }
    }
}
