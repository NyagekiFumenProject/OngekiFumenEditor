using System.Numerics;

namespace OngekiFumenEditor.Kernel.Graphics.DrawCommands.DefaultDrawCommands
{
    /// <summary>
    /// Pushes a new current view matrix while presenting a command list.
    /// </summary>
    public sealed record PushViewMatrixCommand : DrawCommand
    {
        /// <summary>
        /// Initializes a view-matrix push command.
        /// </summary>
        public PushViewMatrixCommand(Matrix4x4 matrix)
        {
            Matrix = matrix;
        }

        /// <summary>
        /// Gets the matrix to use inside the pushed view scope.
        /// </summary>
        public Matrix4x4 Matrix { get; init; }
    }
}
