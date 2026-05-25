using OngekiFumenEditor.Utils.ObjectPool;
using System.Numerics;

namespace OngekiFumenEditor.Kernel.Graphics.DrawCommands.DefaultDrawCommands
{
    /// <summary>
    /// Replaces the current projection matrix while presenting a command list.
    /// </summary>
    public sealed class SetCurrentProjectionMatrixCommand : DrawCommand
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SetCurrentProjectionMatrixCommand"/> class.
        /// </summary>
        public SetCurrentProjectionMatrixCommand()
        {
        }

        internal SetCurrentProjectionMatrixCommand Initialize(Matrix4x4 matrix)
        {
            Matrix = matrix;
            return this;
        }

        /// <summary>
        /// Gets the matrix to use as the current projection matrix.
        /// </summary>
        public Matrix4x4 Matrix { get; private set; }

        /// <inheritdoc />
        protected override void ReturnToPoolCore()
        {
            ObjectPool<SetCurrentProjectionMatrixCommand>.Return(this);
        }
    }
}
