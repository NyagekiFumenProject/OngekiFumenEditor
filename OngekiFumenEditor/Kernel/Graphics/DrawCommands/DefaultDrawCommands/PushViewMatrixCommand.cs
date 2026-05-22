using OngekiFumenEditor.Core.Utils.ObjectPool;
using System.Numerics;

namespace OngekiFumenEditor.Kernel.Graphics.DrawCommands.DefaultDrawCommands
{
    /// <summary>
    /// Pushes a new current view matrix while presenting a command list.
    /// </summary>
    public sealed class PushViewMatrixCommand : DrawCommand
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PushViewMatrixCommand"/> class.
        /// </summary>
        public PushViewMatrixCommand()
        {
        }

        internal PushViewMatrixCommand Initialize(Matrix4x4 matrix)
        {
            Matrix = matrix;
            return this;
        }

        /// <summary>
        /// Gets the matrix to use inside the pushed view scope.
        /// </summary>
        public Matrix4x4 Matrix { get; private set; }

        /// <inheritdoc />
        protected override void ReturnToPoolCore()
        {
            ObjectPool<PushViewMatrixCommand>.Return(this);
        }
    }
}
