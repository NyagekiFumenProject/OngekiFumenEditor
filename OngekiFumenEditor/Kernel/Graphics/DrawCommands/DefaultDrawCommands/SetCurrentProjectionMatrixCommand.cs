using System.Numerics;

namespace OngekiFumenEditor.Kernel.Graphics.DrawCommands.DefaultDrawCommands
{
    /// <summary>
    /// Replaces the current projection matrix while presenting a command list.
    /// </summary>
    public sealed record SetCurrentProjectionMatrixCommand : DrawCommand
    {
        /// <summary>
        /// Initializes a projection-matrix replacement command.
        /// </summary>
        public SetCurrentProjectionMatrixCommand(Matrix4x4 matrix)
        {
            Matrix = matrix;
        }

        /// <summary>
        /// Gets the matrix to use as the current projection matrix.
        /// </summary>
        public Matrix4x4 Matrix { get; init; }
    }
}
