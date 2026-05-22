using System;

namespace OngekiFumenEditor.Kernel.Graphics.DrawCommands
{
    /// <summary>
    /// Base type for a backend-independent render command stored in a draw command list.
    /// </summary>
    public abstract record DrawCommand : IDisposable
    {
        /// <summary>
        /// Releases pooled resources owned by this command.
        /// </summary>
        public virtual void Dispose()
        {
        }
    }
}
