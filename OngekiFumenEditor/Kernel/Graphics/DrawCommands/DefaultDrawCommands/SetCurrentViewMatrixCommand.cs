using OngekiFumenEditor.Core.Utils.ObjectPool;
using System.Numerics;

namespace OngekiFumenEditor.Kernel.Graphics.DrawCommands.DefaultDrawCommands
{
    /// <summary>
    /// Replaces the current view matrix while presenting a command list.
    /// </summary>
    public sealed class SetCurrentViewMatrixCommand : DrawCommand
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SetCurrentViewMatrixCommand"/> class.
        /// </summary>
        public SetCurrentViewMatrixCommand()
        {
        }

        internal SetCurrentViewMatrixCommand Initialize(Matrix4x4 matrix)
        {
            Matrix = matrix;
            return this;
        }

        /// <summary>
        /// Gets the matrix to use as the current view matrix.
        /// </summary>
        public Matrix4x4 Matrix { get; private set; }

        /// <inheritdoc />
        protected override void ReturnToPoolCore()
        {
            ObjectPool<SetCurrentViewMatrixCommand>.Return(this);
        }
    }
}
