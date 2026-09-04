using OngekiFumenEditor.Avalonia.Utils.ObjectPool;
using System;
using System.Collections.Generic;

namespace OngekiFumenEditor.Avalonia.Kernel.Graphics.DrawCommands.DefaultDrawCommands
{
    /// <summary>
    /// Represents a batched circle drawing command.
    /// </summary>
    public sealed class DrawCirclesCommand : DrawCommand
    {
        private IPooledList<CircleInstance> instances;

        public DrawCirclesCommand()
        {
        }

        internal DrawCirclesCommand Initialize(IPooledList<CircleInstance> instances)
        {
            this.instances = instances ?? throw new ArgumentNullException(nameof(instances));
            return this;
        }

        /// <summary>
        /// Gets the circle instances owned by this command.
        /// </summary>
        public IReadOnlyList<CircleInstance> Instances => instances is null ? Array.Empty<CircleInstance>() : instances;

        public override void Dispose()
        {
            instances?.Dispose();
            instances = null;
        }

        protected override void ReturnToPoolCore()
        {
            ObjectPool<DrawCirclesCommand>.Return(this);
        }
    }
}
