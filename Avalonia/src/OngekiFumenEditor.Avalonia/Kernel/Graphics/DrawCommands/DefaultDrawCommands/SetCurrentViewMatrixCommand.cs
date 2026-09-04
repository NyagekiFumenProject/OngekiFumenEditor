using OpenTK.Mathematics;
using OngekiFumenEditor.Avalonia.Utils.ObjectPool;
using System;

namespace OngekiFumenEditor.Avalonia.Kernel.Graphics.DrawCommands.DefaultDrawCommands
{
    /// <summary>
    /// Replaces the current view matrix while presenting a command list.
    /// </summary>
    public sealed class SetCurrentViewMatrixCommand : DrawCommand, IComparable<SetCurrentViewMatrixCommand>
    {
        public SetCurrentViewMatrixCommand()
        {
        }

        internal SetCurrentViewMatrixCommand Initialize(Matrix4 matrix)
        {
            Matrix = matrix;
            return this;
        }

        /// <summary>
        /// Gets the matrix to use as the current view matrix.
        /// </summary>
        public Matrix4 Matrix { get; private set; }

        protected override void ReturnToPoolCore()
        {
            ObjectPool<SetCurrentViewMatrixCommand>.Return(this);
        }

        public int CompareTo(SetCurrentViewMatrixCommand other)
        {
            return Matrix.Equals(other.Matrix) ? 0 : -1;
        }
    }
}