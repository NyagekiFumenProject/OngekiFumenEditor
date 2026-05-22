using OngekiFumenEditor.Kernel.Graphics.DrawCommands.DefaultDrawCommands;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace OngekiFumenEditor.Kernel.Graphics.DrawCommands
{
    /// <summary>
    /// Collects backend-independent draw commands and produces immutable command-list snapshots.
    /// </summary>
    public interface IDrawCommandListBuilder : IDisposable
    {
        /// <summary>
        /// Sets the frame clean color. Passing null means this frame should not be cleared by the command-list presenter.
        /// </summary>
        void SetCleanColor(Vector4? color);

        /// <summary>
        /// Sets the frame viewport and render scale captured by the next command list.
        /// </summary>
        void SetViewport(float viewWidth, float viewHeight, float renderScaleX = 1, float renderScaleY = 1);

        /// <summary>
        /// Replaces the current model matrix and records a state-change command.
        /// </summary>
        void SetCurrentModelMatrix(Matrix4x4 matrix);

        /// <summary>
        /// Replaces the current view matrix and records a state-change command.
        /// </summary>
        void SetCurrentViewMatrix(Matrix4x4 matrix);

        /// <summary>
        /// Replaces the current projection matrix and records a state-change command.
        /// </summary>
        void SetCurrentProjectionMatrix(Matrix4x4 matrix);

        /// <summary>
        /// Pushes a model matrix scope and records a state-change command.
        /// </summary>
        void PushModelMatrix(Matrix4x4 matrix);

        /// <summary>
        /// Pushes a view matrix scope and records a state-change command.
        /// </summary>
        void PushViewMatrix(Matrix4x4 matrix);

        /// <summary>
        /// Pushes a projection matrix scope and records a state-change command.
        /// </summary>
        void PushProjectionMatrix(Matrix4x4 matrix);

        /// <summary>
        /// Pops the current model matrix scope and records a state-change command.
        /// </summary>
        void PopModelMatrix();

        /// <summary>
        /// Pops the current view matrix scope and records a state-change command.
        /// </summary>
        void PopViewMatrix();

        /// <summary>
        /// Pops the current projection matrix scope and records a state-change command.
        /// </summary>
        void PopProjectionMatrix();

        /// <summary>
        /// Adds a line-drawing command.
        /// </summary>
        void DrawLines(IEnumerable<ILineDrawing.LineVertex> points, float lineWidth);

        /// <summary>
        /// Adds a simple-line drawing command.
        /// </summary>
        void DrawSimpleLines(IEnumerable<ILineDrawing.LineVertex> points, float lineWidth);

        /// <summary>
        /// Adds a texture drawing command.
        /// </summary>
        void DrawTexture(IImage texture, IEnumerable<TextureInstance> instances);

        /// <summary>
        /// Adds a batch texture drawing command.
        /// </summary>
        void DrawBatchTexture(IImage texture, IEnumerable<TextureInstance> instances);

        /// <summary>
        /// Adds a highlighted batch texture drawing command.
        /// </summary>
        void DrawHighlightBatchTexture(IImage texture, IEnumerable<TextureInstance> instances);

        /// <summary>
        /// Adds a single circle drawing command.
        /// </summary>
        void DrawCircle(Vector2 point, Vector4 color, bool isSolid, float radius, float hollowLineWidth = 0);

        /// <summary>
        /// Adds a batched circle drawing command.
        /// </summary>
        void DrawCircles(IEnumerable<CircleInstance> instances);

        /// <summary>
        /// Adds a polygon drawing command.
        /// </summary>
        void DrawPolygon(Primitive primitive, IEnumerable<PolygonVertex> vertices);

        /// <summary>
        /// Adds a string drawing command without measuring text output.
        /// </summary>
        void DrawString(string text, Vector2 pos, Vector2 scale, int fontSize, float rotate, Vector4 color, Vector2 origin, IStringDrawing.StringStyle style, IStringDrawing.IFontHandle handle);

        /// <summary>
        /// Adds a beam drawing command.
        /// </summary>
        void DrawBeam(IImage texture, int width, float x, float progress, Vector4 color, float rotate, float judgeOffset);

        /// <summary>
        /// Copies draw commands into this builder by replaying them in order.
        /// </summary>
        void DrawDrawCommandList(IEnumerable<DrawCommand> drawCommands);

        /// <summary>
        /// Transfers the current command buffer into a command-list snapshot and resets the builder state.
        /// </summary>
        DrawCommandList GetDrawCommandList();

        /// <summary>
        /// Clears pending commands and resets frame state without creating a command list.
        /// </summary>
        void Clear();
    }
}
