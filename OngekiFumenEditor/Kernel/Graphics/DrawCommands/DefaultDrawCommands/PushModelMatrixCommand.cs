using OngekiFumenEditor.Utils.ObjectPool;
using System.Numerics;

namespace OngekiFumenEditor.Kernel.Graphics.DrawCommands.DefaultDrawCommands
{
    /// <summary>
    /// Pushes a new current model matrix while presenting a command list.
    /// </summary>
    public sealed class PushModelMatrixCommand : DrawCommand
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PushModelMatrixCommand"/> class.
        /// </summary>
        public PushModelMatrixCommand()
        {
        }

        internal PushModelMatrixCommand Initialize(Matrix4x4 matrix)
        {
            Matrix = matrix;
            return this;
        }

        /// <summary>
        /// Gets the matrix to use inside the pushed model scope.
        /// </summary>
        public Matrix4x4 Matrix { get; private set; }

        /// <inheritdoc />
        protected override void ReturnToPoolCore()
        {
            ObjectPool<PushModelMatrixCommand>.Return(this);
        }
    }
}
