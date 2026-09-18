using System;

namespace OngekiFumenEditor.Kernel.Graphics.DrawCommands
{
    /// <summary>
    /// Base type for a backend-independent render command stored in a draw command list.
    /// </summary>
    public abstract class DrawCommand : IDisposable
    {
        /// <summary>
        /// Releases pooled resources owned by this command.
        /// </summary>
        public virtual void Dispose()
        {
        }

        internal void DisposeAndReturnSelf()
        {
            Dispose();
            ReturnToPoolCore();
        }

        /// <summary>
        /// Returns this command object to its backing pool after owned resources are released.
        /// </summary>
        protected virtual void ReturnToPoolCore()
        {
        }
    }
}
