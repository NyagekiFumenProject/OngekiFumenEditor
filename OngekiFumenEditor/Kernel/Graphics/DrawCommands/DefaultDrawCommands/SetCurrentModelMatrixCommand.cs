using System.Numerics;

namespace OngekiFumenEditor.Kernel.Graphics.DrawCommands.DefaultDrawCommands
{
    /// <summary>
    /// Replaces the current model matrix while presenting a command list.
    /// </summary>
    public sealed record SetCurrentModelMatrixCommand : DrawCommand
    {
        /// <summary>
        /// Initializes a model-matrix replacement command.
        /// </summary>
        public SetCurrentModelMatrixCommand(Matrix4x4 matrix)
        {
            Matrix = matrix;
        }

        /// <summary>
        /// Gets the matrix to use as the current model matrix.
        /// </summary>
        public Matrix4x4 Matrix { get; init; }
    }
}
