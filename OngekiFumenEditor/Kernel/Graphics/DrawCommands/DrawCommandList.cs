using OngekiFumenEditor.Utils.ObjectPool;
using System;
using System.Collections.Generic;

namespace OngekiFumenEditor.Kernel.Graphics.DrawCommands
{
    /// <summary>
    /// Immutable snapshot of draw commands and frame render state.
    /// </summary>
    public sealed class DrawCommandList : IDisposable
    {
        private enum LifecycleState
        {
            Normal,
            PresentApplying,
            DisposeRequested,
            Disposed
        }

        private IPooledList<DrawCommand> commands;
        private LifecycleState state = LifecycleState.Normal;

        internal DrawCommandList(IPooledList<DrawCommand> commands, DrawCommandListFrameState frameState)
        {
            this.commands = commands ?? throw new ArgumentNullException(nameof(commands));
            FrameState = frameState;
        }

        /// <summary>
        /// Gets the command sequence. The collection appears empty after disposal.
        /// </summary>
        public IReadOnlyList<DrawCommand> Commands => commands is null ? Array.Empty<DrawCommand>() : commands;

        /// <summary>
        /// Gets the frame-level state captured when this list was created.
        /// </summary>
        public DrawCommandListFrameState FrameState { get; }

        /// <summary>
        /// Gets whether this command list has released its pooled resources.
        /// </summary>
        public bool IsDisposed
        {
            get
            {
                return state == LifecycleState.Disposed;
            }
        }

        internal bool TryBeginPresent()
        {
            if (state != LifecycleState.Normal)
                return false;

            state = LifecycleState.PresentApplying;
            return true;
        }

        internal void EndPresent()
        {
            if (state == LifecycleState.DisposeRequested)
            {
                DisposeCore();
                state = LifecycleState.Disposed;
                return;
            }

            if (state == LifecycleState.PresentApplying)
                state = LifecycleState.Normal;
        }

        /// <summary>
        /// Releases all commands and pooled collections owned by this list.
        /// </summary>
        public void Dispose()
        {
            if (state == LifecycleState.Disposed)
                return;

            if (state == LifecycleState.PresentApplying)
            {
                state = LifecycleState.DisposeRequested;
                return;
            }

            DisposeCore();
            state = LifecycleState.Disposed;
        }

        private void DisposeCore()
        {
            if (commands is null)
                return;

            foreach (var command in commands)
                command?.DisposeAndReturnSelf();

            commands.Dispose();
            commands = null;
        }
    }
}
