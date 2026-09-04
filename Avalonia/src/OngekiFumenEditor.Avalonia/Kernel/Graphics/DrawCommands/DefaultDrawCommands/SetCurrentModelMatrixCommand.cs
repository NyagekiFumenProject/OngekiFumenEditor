using OpenTK.Mathematics;
using OngekiFumenEditor.Avalonia.Utils.ObjectPool;
using System;

namespace OngekiFumenEditor.Avalonia.Kernel.Graphics.DrawCommands.DefaultDrawCommands
{
    /// <summary>
    /// Replaces the current model matrix while presenting a command list.
    /// </summary>
    public sealed class SetCurrentModelMatrixCommand : DrawCommand, IComparable<SetCurrentModelMatrixCommand>
    {
        public SetCurrentModelMatrixCommand()
        {
        }

        internal SetCurrentModelMatrixCommand Initialize(Matrix4 matrix)
        {
            Matrix = matrix;
            return this;
        }

        /// <summary>
        /// Gets the matrix to use as the current model matrix.
        /// </summary>
        public Matrix4 Matrix { get; private set; }

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