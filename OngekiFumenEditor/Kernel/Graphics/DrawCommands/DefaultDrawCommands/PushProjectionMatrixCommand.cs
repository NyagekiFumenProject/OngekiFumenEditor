using System.Numerics;

namespace OngekiFumenEditor.Kernel.Graphics.DrawCommands.DefaultDrawCommands
{
    /// <summary>
    /// Pushes a new current projection matrix while presenting a command list.
    /// </summary>
    public sealed record PushProjectionMatrixCommand : DrawCommand
    {
        /// <summary>
        /// Initializes a projection-matrix push command.
        /// </summary>
        public PushProjectionMatrixCommand(Matrix4x4 matrix)
        {
            Matrix = matrix;
        }

        /// <summary>
        /// Gets the matrix to use inside the pushed projection scope.
        /// </summary>
        public Matrix4x4 Matrix { get; init; }
    }
}
