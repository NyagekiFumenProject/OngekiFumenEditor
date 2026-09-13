using OngekiFumenEditor.Avalonia.Kernel.Graphics.DrawCommands;
using OngekiFumenEditor.Avalonia.Kernel.Graphics.DrawCommands.DefaultDrawCommands;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Graphics.Drawing;
using OngekiFumenEditor.Avalonia.Kernel.Graphics.Performence;
using OngekiFumenEditor.Avalonia.Kernel.Graphics.Skia.Drawing.BeamDrawing;
using OngekiFumenEditor.Avalonia.Kernel.Graphics.Skia.Drawing.CircleDrawing;
using OngekiFumenEditor.Avalonia.Kernel.Graphics.Skia.Drawing.LineDrawing;
using OngekiFumenEditor.Avalonia.Kernel.Graphics.Skia.Drawing.PolygonDrawing;
using OngekiFumenEditor.Avalonia.Kernel.Graphics.Skia.Drawing.StringDrawing;
using OngekiFumenEditor.Avalonia.Kernel.Graphics.Skia.Drawing.TextureDrawing;
using SkiaSharp;
using System;
using System.Collections.Generic;
using OpenTK.Mathematics;

namespace OngekiFumenEditor.Avalonia.Kernel.Graphics.Skia
{
    /// <summary>
    /// Replays a frame's draw commands onto a Skia canvas.
    /// <para>
    /// The instance is owned by the render manager and cached per render context
    /// (PERF-RND-017 / RND-20): constructing it builds the target/drawing contexts, the backend
    /// drawings and their pools/paints, so rebuilding it every frame both churned allocations and
    /// reset every cross-frame cache those backends hold. Callers must therefore use the
    /// <see cref="BeginFrame"/> / <see cref="Present"/> / <see cref="EndFrame"/> sequence per frame
    /// and let the owning context dispose the instance at teardown.
    /// </para>
    /// </summary>
    internal sealed class SkiaDrawCommandListReplay : IDisposable
    {
        private readonly ReplayDrawingContext drawingContext;
        private readonly DrawingTargetContext targetContext;
        private readonly NewSkiaLineDrawing lineDrawing;
        private readonly DefaultSkiaTextureDrawing textureDrawing;
        private readonly DefaultSkiaBatchTextureDrawing batchTextureDrawing;
        private readonly DefaultSkiaHighlightBatchTextureDrawing highlightBatchTextureDrawing;
        private readonly DefaultSkiaCircleDrawing circleDrawing;
        private readonly DefaultSkiaPolygonDrawing polygonDrawing;
        private readonly DefaultSkiaStringDrawing stringDrawing;
        private readonly DefaultSkiaBeamDrawing beamDrawing;
        private readonly IRenderContext renderContext;

        /// <summary>
        /// The leased canvas of the frame currently being presented. Only valid between
        /// <see cref="BeginFrame"/> and <see cref="EndFrame"/>; cleared afterwards so a cached,
        /// long-lived replay never roots an expired lease.
        /// </summary>
        private SKCanvas canvas;

        /// <summary>
        /// Clean color captured by <see cref="BeginFrame"/>; null means the target must not be cleared.
        /// </summary>
        private System.Numerics.Vector4? frameCleanColor;

        private readonly Stack<Matrix4> modelMatrixStack = new();
        private readonly Stack<Matrix4> viewMatrixStack = new();
        private readonly Stack<Matrix4> projectionMatrixStack = new();

        private Matrix4 currentModelMatrix;
        private Matrix4 currentViewMatrix;
        private Matrix4 currentProjectionMatrix;

        private bool disposed;

        public SkiaDrawCommandListReplay(DefaultSkiaDrawingManagerImpl manager, IRenderContext renderContext)
        {
            ArgumentNullException.ThrowIfNull(manager);
            ArgumentNullException.ThrowIfNull(renderContext);

            this.renderContext = renderContext;

            targetContext = new DrawingTargetContext();
            drawingContext = new ReplayDrawingContext(renderContext, targetContext);

            lineDrawing = new NewSkiaLineDrawing(manager);
            textureDrawing = new DefaultSkiaTextureDrawing(manager);
            batchTextureDrawing = new DefaultSkiaBatchTextureDrawing(manager);
            highlightBatchTextureDrawing = new DefaultSkiaHighlightBatchTextureDrawing(manager);
            circleDrawing = new DefaultSkiaCircleDrawing(manager);
            polygonDrawing = new DefaultSkiaPolygonDrawing(manager);
            stringDrawing = new DefaultSkiaStringDrawing(manager);
            beamDrawing = new DefaultSkiaBeamDrawing(manager);
        }

        /// <summary>
        /// Binds the frame's canvas and resets all per-frame replay state. Must be called once per
        /// frame before <see cref="Present"/>.
        /// <para>
        /// The matrix stacks are cleared explicitly instead of relying on the command stream being
        /// balanced: a frame that threw midway used to leave entries behind, and with a shared
        /// instance those leftovers would otherwise be popped by the next frame.
        /// </para>
        /// </summary>
        public void BeginFrame(SKCanvas canvas, DrawCommandListFrameState frameState)
        {
            ArgumentNullException.ThrowIfNull(canvas);

            this.canvas = canvas;
            frameCleanColor = frameState.CleanColor;

            modelMatrixStack.Clear();
            viewMatrixStack.Clear();
            projectionMatrixStack.Clear();

            currentModelMatrix = frameState.ModelMatrix;
            currentViewMatrix = frameState.ViewMatrix;
            currentProjectionMatrix = frameState.ProjectionMatrix;

            targetContext.ViewWidth = frameState.ViewWidth;
            targetContext.ViewHeight = frameState.ViewHeight;
            targetContext.ViewMatrix = currentViewMatrix;
            targetContext.ProjectionMatrix = currentProjectionMatrix;
            targetContext.ViewRelativeRect = new VisibleRect(new Vector2(frameState.ViewWidth, 0), new Vector2(0, frameState.ViewHeight));
        }

        /// <summary>
        /// Releases the frame's canvas reference. Call once per frame after <see cref="Present"/>.
        /// </summary>
        public void EndFrame()
        {
            canvas = null;
            frameCleanColor = null;
        }

        public void Present(IReadOnlyList<DrawCommand> commands)
        {
            ArgumentNullException.ThrowIfNull(commands);

            if (canvas is null)
                throw new InvalidOperationException($"{nameof(BeginFrame)} must be called before {nameof(Present)}.");

            if (frameCleanColor is { } cleanColor)
                canvas.Clear(new SKColorF(cleanColor.X, cleanColor.Y, cleanColor.Z, cleanColor.W));

            var perfomenceMonitor = drawingContext.PerfomenceMonitor;
            foreach (var command in commands)
            {
                perfomenceMonitor.OnBeginDrawCommand(command);
                try
                {
                    Present(command);
                }
                finally
                {
                    perfomenceMonitor.OnEndDrawCommand(command);
                }
            }
        }

        private void Present(DrawCommand command)
        {
            switch (command)
            {
                case SetCurrentModelMatrixCommand setCurrentModelMatrixCommand:
                    currentModelMatrix = setCurrentModelMatrixCommand.Matrix;
                    break;
                case SetCurrentViewMatrixCommand setCurrentViewMatrixCommand:
                    currentViewMatrix = setCurrentViewMatrixCommand.Matrix;
                    targetContext.ViewMatrix = currentViewMatrix;
                    break;
                case SetCurrentProjectionMatrixCommand setCurrentProjectionMatrixCommand:
                    currentProjectionMatrix = setCurrentProjectionMatrixCommand.Matrix;
                    targetContext.ProjectionMatrix = currentProjectionMatrix;
                    break;
                case SetCurrentRectCommand setCurrentRectCommand:
                    targetContext.ViewRelativeRect = setCurrentRectCommand.Rect;
                    break;
                case PushModelMatrixCommand pushModelMatrixCommand:
                    modelMatrixStack.Push(currentModelMatrix);
                    currentModelMatrix = pushModelMatrixCommand.Matrix;
                    break;
                case PushViewMatrixCommand pushViewMatrixCommand:
                    viewMatrixStack.Push(currentViewMatrix);
                    currentViewMatrix = pushViewMatrixCommand.Matrix;
                    targetContext.ViewMatrix = currentViewMatrix;
                    break;
                case PushProjectionMatrixCommand pushProjectionMatrixCommand:
                    projectionMatrixStack.Push(currentProjectionMatrix);
                    currentProjectionMatrix = pushProjectionMatrixCommand.Matrix;
                    targetContext.ProjectionMatrix = currentProjectionMatrix;
                    break;
                case PopModelMatrixCommand:
                    currentModelMatrix = modelMatrixStack.Pop();
                    break;
                case PopViewMatrixCommand:
                    currentViewMatrix = viewMatrixStack.Pop();
                    targetContext.ViewMatrix = currentViewMatrix;
                    break;
                case PopProjectionMatrixCommand:
                    currentProjectionMatrix = projectionMatrixStack.Pop();
                    targetContext.ProjectionMatrix = currentProjectionMatrix;
                    break;
                case DrawLinesCommand drawLinesCommand:
                    lineDrawing.Draw(drawingContext, drawLinesCommand.Points, drawLinesCommand.LineWidth);
                    break;
                case DrawSimpleLinesCommand drawSimpleLinesCommand:
                    lineDrawing.Draw(drawingContext, drawSimpleLinesCommand.Points, drawSimpleLinesCommand.LineWidth);
                    break;
                case DrawTextureCommand drawTextureCommand:
                    textureDrawing.Draw(drawingContext, drawTextureCommand.Texture, ToInstanceTuples(drawTextureCommand.Instances));
                    break;
                case DrawBatchTextureCommand drawBatchTextureCommand:
                    batchTextureDrawing.Draw(drawingContext, drawBatchTextureCommand.Texture, ToInstanceTuples(drawBatchTextureCommand.Instances));
                    break;
                case DrawHighlightBatchTextureCommand drawHighlightBatchTextureCommand:
                    highlightBatchTextureDrawing.Draw(drawingContext, drawHighlightBatchTextureCommand.Texture, ToInstanceTuples(drawHighlightBatchTextureCommand.Instances));
                    break;
                case DrawCirclesCommand drawCirclesCommand:
                    PresentCircles(drawCirclesCommand.Instances);
                    break;
                case DrawPolygonCommand drawPolygonCommand:
                    PresentPolygon(drawPolygonCommand.Primitive, drawPolygonCommand.Vertices);
                    break;
                case DrawStringCommand drawStringCommand:
                    stringDrawing.Draw(drawStringCommand.Text, drawStringCommand.Position, drawStringCommand.Scale, drawStringCommand.FontSize, drawStringCommand.Rotate, drawStringCommand.Color, drawStringCommand.Origin, drawStringCommand.Style, drawingContext, drawStringCommand.FontHandle, out _);
                    break;
                case DrawBeamCommand drawBeamCommand:
                    beamDrawing.Draw(drawingContext, drawBeamCommand.Texture, drawBeamCommand.Width, drawBeamCommand.X, drawBeamCommand.Progress, new Vector4(drawBeamCommand.Color.X, drawBeamCommand.Color.Y, drawBeamCommand.Color.Z, drawBeamCommand.Color.W), drawBeamCommand.Rotate, drawBeamCommand.JudgeOffset);
                    break;
                default:
                    throw new NotSupportedException($"Unsupported draw command type: {command.GetType().FullName}");
            }
        }

        private static IEnumerable<(System.Numerics.Vector2 size, System.Numerics.Vector2 position, float rotation, System.Numerics.Vector4 color)> ToInstanceTuples(IReadOnlyList<TextureInstance> instances)
        {
            foreach (var instance in instances)
                yield return (instance.Size, instance.Position, instance.Rotation, instance.Color);
        }

        private void PresentCircles(IReadOnlyList<CircleInstance> instances)
        {
            if (instances.Count == 0)
                return;

            circleDrawing.Begin(drawingContext);
            try
            {
                foreach (var instance in instances)
                    circleDrawing.Post(instance.Point, instance.Color, instance.IsSolid, instance.Radius, instance.HollowLineWidth);
            }
            finally
            {
                circleDrawing.End();
            }
        }

        private void PresentPolygon(Primitive primitive, IReadOnlyList<PolygonVertex> vertices)
        {
            if (vertices.Count == 0)
                return;

            polygonDrawing.Begin(drawingContext, primitive);
            try
            {
                foreach (var vertex in vertices)
                    polygonDrawing.PostPoint(vertex.Point, vertex.Color);
            }
            finally
            {
                polygonDrawing.End();
            }
        }

        /// <summary>
        /// Releases every native resource owned by this replay. Called by the render manager when the
        /// owning render context is released; the instance is cached across frames, so this is the
        /// only place those resources are freed.
        /// </summary>
        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
            canvas = null;
            frameCleanColor = null;

            lineDrawing.Dispose();
            stringDrawing.Dispose();
            textureDrawing.Dispose();
        }

        private sealed class ReplayDrawingContext : IDrawingContext
        {
            private readonly DrawingTargetContext currentDrawingTargetContext;

            public ReplayDrawingContext(IRenderContext renderContext, DrawingTargetContext targetContext)
            {
                RenderContext = renderContext;
                currentDrawingTargetContext = targetContext;
            }

            public DrawingTargetContext CurrentDrawingTargetContext => currentDrawingTargetContext;

            public IPerfomenceMonitor PerfomenceMonitor => RenderContext.PerfomenceMonitor ?? DummyPerformenceMonitor.Instance;

            public IRenderContext RenderContext { get; }

            public void Render(TimeSpan ts)
            {
            }
        }
    }
}
