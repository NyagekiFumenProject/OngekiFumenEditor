using OngekiFumenEditor.Core.Utils.ObjectPool;
using System.Numerics;

namespace OngekiFumenEditor.Kernel.Graphics.DrawCommands.DefaultDrawCommands
{
    /// <summary>
    /// Pushes a new current projection matrix while presenting a command list.
    /// </summary>
    public sealed class PushProjectionMatrixCommand : DrawCommand
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PushProjectionMatrixCommand"/> class.
        /// </summary>
        public PushProjectionMatrixCommand()
        {
        }

        internal PushProjectionMatrixCommand Initialize(Matrix4x4 matrix)
        {
            Matrix = matrix;
            return this;
        }

        /// <summary>
        /// Gets the matrix to use inside the pushed projection scope.
        /// </summary>
        public Matrix4x4 Matrix { get; private set; }

        /// <inheritdoc />
        protected override void ReturnToPoolCore()
        {
            ObjectPool<PushProjectionMatrixCommand>.Return(this);
        }
    }
}
