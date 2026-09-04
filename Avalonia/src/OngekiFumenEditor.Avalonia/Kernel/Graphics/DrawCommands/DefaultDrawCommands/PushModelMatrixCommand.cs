using OpenTK.Mathematics;
using OngekiFumenEditor.Avalonia.Utils.ObjectPool;
using System;

namespace OngekiFumenEditor.Avalonia.Kernel.Graphics.DrawCommands.DefaultDrawCommands
{
    /// <summary>
    /// Pushes a new current model matrix while presenting a command list.
    /// </summary>
    public sealed class PushModelMatrixCommand : DrawCommand
    {
        public PushModelMatrixCommand()
        {
        }

        internal PushModelMatrixCommand Initialize(Matrix4 matrix)
        {
            Matrix = matrix;
            return this;
        }

        /// <summary>
        /// Gets the matrix to use inside the pushed model scope.
        /// </summary>
        public Matrix4 Matrix { get; private set; }

        protected override void ReturnToPoolCore()
        {
            ObjectPool<PushModelMatrixCommand>.Return(this);
        }
    }
}