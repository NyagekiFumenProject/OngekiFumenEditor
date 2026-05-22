using System.Numerics;

namespace OngekiFumenEditor.Kernel.Graphics.DrawCommands.DefaultDrawCommands
{
    /// <summary>
    /// Pushes a new current model matrix while presenting a command list.
    /// </summary>
    public sealed record PushModelMatrixCommand : DrawCommand
    {
        /// <summary>
        /// Initializes a model-matrix push command.
        /// </summary>
        public PushModelMatrixCommand(Matrix4x4 matrix)
        {
            Matrix = matrix;
        }

        /// <summary>
        /// Gets the matrix to use inside the pushed model scope.
        /// </summary>
        public Matrix4x4 Matrix { get; init; }
    }
}
