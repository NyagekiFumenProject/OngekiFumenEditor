using OpenTK.Mathematics;
using OngekiFumenEditor.Avalonia.Utils.ObjectPool;
using System;

namespace OngekiFumenEditor.Avalonia.Kernel.Graphics.DrawCommands.DefaultDrawCommands
{
    /// <summary>
    /// Pushes a new current view matrix while presenting a command list.
    /// </summary>
    public sealed class PushViewMatrixCommand : DrawCommand
    {
        public PushViewMatrixCommand()
        {
        }

        internal PushViewMatrixCommand Initialize(Matrix4 matrix)
        {
            Matrix = matrix;
            return this;
        }

        /// <summary>
        /// Gets the matrix to use inside the pushed view scope.
        /// </summary>
        public Matrix4 Matrix { get; private set; }

        protected override void ReturnToPoolCore()
        {
            ObjectPool<PushViewMatrixCommand>.Return(this);
        }
    }
}