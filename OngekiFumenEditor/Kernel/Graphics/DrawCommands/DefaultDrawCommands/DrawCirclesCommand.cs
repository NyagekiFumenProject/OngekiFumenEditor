using OngekiFumenEditor.Core.Utils.ObjectPool;
using System;
using System.Collections.Generic;

namespace OngekiFumenEditor.Kernel.Graphics.DrawCommands.DefaultDrawCommands
{
    /// <summary>
    /// Represents a batched circle drawing command.
    /// </summary>
    public sealed record DrawCirclesCommand : DrawCommand
    {
        private IPooledList<CircleInstance> instances;

        /// <summary>
        /// Initializes a batched circle drawing command.
        /// </summary>
        public DrawCirclesCommand(IPooledList<CircleInstance> instances)
        {
            this.instances = instances ?? throw new ArgumentNullException(nameof(instances));
        }

        /// <summary>
        /// Gets the circle instances owned by this command.
        /// </summary>
        public IReadOnlyList<CircleInstance> Instances => instances is null ? Array.Empty<CircleInstance>() : instances;

        /// <summary>
        /// Releases the pooled circle list owned by this command.
        /// </summary>
        public override void Dispose()
        {
            instances?.Dispose();
            instances = null;
        }
    }
}
