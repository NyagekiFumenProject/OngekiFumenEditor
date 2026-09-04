using OpenTK.Mathematics;
using OngekiFumenEditor.Avalonia.Utils.ObjectPool;
using System;

namespace OngekiFumenEditor.Avalonia.Kernel.Graphics.DrawCommands.DefaultDrawCommands
{
    /// <summary>
    /// Pushes a new current projection matrix while presenting a command list.
    /// </summary>
    public sealed class PushProjectionMatrixCommand : DrawCommand
    {
        public PushProjectionMatrixCommand()
        {
        }

        internal PushProjectionMatrixCommand Initialize(Matrix4 matrix)
        {
            Matrix = matrix;
            return this;
        }

        /// <summary>
        /// Gets the matrix to use inside the pushed projection scope.
        /// </summary>
        public Matrix4 Matrix { get; private set; }

        protected override void ReturnToPoolCore()
        {
            ObjectPool<PushProjectionMatrixCommand>.Return(this);
        }
    }
}