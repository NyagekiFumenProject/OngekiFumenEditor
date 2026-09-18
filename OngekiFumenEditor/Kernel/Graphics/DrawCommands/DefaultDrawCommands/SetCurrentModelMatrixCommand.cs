using OngekiFumenEditor.Utils.ObjectPool;
using System;
using System.Numerics;

namespace OngekiFumenEditor.Kernel.Graphics.DrawCommands.DefaultDrawCommands
{
    /// <summary>
    /// Replaces the current model matrix while presenting a command list.
    /// </summary>
    public sealed class SetCurrentModelMatrixCommand : DrawCommand, IComparable<SetCurrentModelMatrixCommand>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SetCurrentModelMatrixCommand"/> class.
        /// </summary>
        public SetCurrentModelMatrixCommand()
        {
        }

        internal SetCurrentModelMatrixCommand Initialize(Matrix4x4 matrix)
        {
            Matrix = matrix;
            return this;
        }

        /// <summary>
        /// Gets the matrix to use as the current model matrix.
        /// </summary>
        public Matrix4x4 Matrix { get; private set; }

        /// <inheritdoc />
        protected override void ReturnToPoolCore()
        {
            ObjectPool<SetCurrentModelMatrixCommand>.Return(this);
        }

        public int CompareTo(SetCurrentModelMatrixCommand other)
        {
            return Matrix.Equals(other.Matrix) ? 0 : -1;
        }
    }
}
