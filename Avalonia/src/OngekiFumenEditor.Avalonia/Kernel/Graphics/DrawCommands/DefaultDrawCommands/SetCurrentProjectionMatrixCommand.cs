using OpenTK.Mathematics;
using OngekiFumenEditor.Avalonia.Utils.ObjectPool;
using System;

namespace OngekiFumenEditor.Avalonia.Kernel.Graphics.DrawCommands.DefaultDrawCommands
{
    /// <summary>
    /// Replaces the current projection matrix while presenting a command list.
    /// </summary>
    public sealed class SetCurrentProjectionMatrixCommand : DrawCommand, IComparable<SetCurrentProjectionMatrixCommand>
    {
        public SetCurrentProjectionMatrixCommand()
        {
        }

        internal SetCurrentProjectionMatrixCommand Initialize(Matrix4 matrix)
        {
            Matrix = matrix;
            return this;
        }

        /// <summary>
        /// Gets the matrix to use as the current projection matrix.
        /// </summary>
        public Matrix4 Matrix { get; private set; }

        protected override void ReturnToPoolCore()
        {
            ObjectPool<SetCurrentProjectionMatrixCommand>.Return(this);
        }

        public int CompareTo(SetCurrentProjectionMatrixCommand other)
        {
            return Matrix.Equals(other.Matrix) ? 0 : -1;
        }
    }
}