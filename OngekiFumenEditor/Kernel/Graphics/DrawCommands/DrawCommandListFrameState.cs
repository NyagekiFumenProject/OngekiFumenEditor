using System.Numerics;

namespace OngekiFumenEditor.Kernel.Graphics.DrawCommands
{
    /// <summary>
    /// Captures frame-level render state that applies to a draw command list snapshot.
    /// </summary>
    public readonly record struct DrawCommandListFrameState
    {
        /// <summary>
        /// Initializes a new frame-state snapshot.
        /// </summary>
        public DrawCommandListFrameState(Vector4? cleanColor, float viewWidth, float viewHeight, float renderScaleX, float renderScaleY, Matrix4x4 modelMatrix, Matrix4x4 viewMatrix, Matrix4x4 projectionMatrix)
        {
            CleanColor = cleanColor;
            ViewWidth = viewWidth;
            ViewHeight = viewHeight;
            RenderScaleX = renderScaleX;
            RenderScaleY = renderScaleY;
            ModelMatrix = modelMatrix;
            ViewMatrix = viewMatrix;
            ProjectionMatrix = projectionMatrix;
        }

        /// <summary>
        /// Gets the optional clear color; null means the target should not be cleared by the command presenter.
        /// </summary>
        public Vector4? CleanColor { get; init; }

        /// <summary>
        /// Gets the logical viewport width captured for this frame.
        /// </summary>
        public float ViewWidth { get; init; }

        /// <summary>
        /// Gets the logical viewport height captured for this frame.
        /// </summary>
        public float ViewHeight { get; init; }

        /// <summary>
        /// Gets the horizontal render scale captured for this frame.
        /// </summary>
        public float RenderScaleX { get; init; }

        /// <summary>
        /// Gets the vertical render scale captured for this frame.
        /// </summary>
        public float RenderScaleY { get; init; }

        /// <summary>
        /// Gets the current model matrix captured when the list was built.
        /// </summary>
        public Matrix4x4 ModelMatrix { get; init; }

        /// <summary>
        /// Gets the current view matrix captured when the list was built.
        /// </summary>
        public Matrix4x4 ViewMatrix { get; init; }

        /// <summary>
        /// Gets the current projection matrix captured when the list was built.
        /// </summary>
        public Matrix4x4 ProjectionMatrix { get; init; }
    }
}
