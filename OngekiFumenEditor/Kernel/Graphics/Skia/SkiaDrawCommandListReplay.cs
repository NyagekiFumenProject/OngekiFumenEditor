using OngekiFumenEditor.Kernel.Graphics.DrawCommands;
using OngekiFumenEditor.Kernel.Graphics.DrawCommands.DefaultDrawCommands;
using OngekiFumenEditor.Kernel.Graphics.Skia.Drawing.BeamDrawing;
using OngekiFumenEditor.Kernel.Graphics.Skia.Drawing.CircleDrawing;
using OngekiFumenEditor.Kernel.Graphics.Skia.Drawing.LineDrawing;
using OngekiFumenEditor.Kernel.Graphics.Skia.Drawing.PolygonDrawing;
using OngekiFumenEditor.Kernel.Graphics.Skia.Drawing.StringDrawing;
using OngekiFumenEditor.Kernel.Graphics.Skia.Drawing.TextureDrawing;
using OngekiFumenEditor.Modules.FumenVisualEditor.Graphics.Drawing;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace OngekiFumenEditor.Kernel.Graphics.Skia
{
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
        private readonly IPerfomenceMonitor perfomenceMonitor;

        private readonly Stack<Matrix4x4> modelMatrixStack = new();
        private readonly Stack<Matrix4x4> viewMatrixStack = new();
        private readonly Stack<Matrix4x4> projectionMatrixStack = new();

        private Matrix4x4 currentModelMatrix;
        private Matrix4x4 currentViewMatrix;
        private Matrix4x4 currentProjectionMatrix;

        public SkiaDrawCommandListReplay(DefaultSkiaDrawingManagerImpl manager, IRenderContext renderContext, DrawCommandListFrameState frameState, SKCanvas canvas, IPerfomenceMonitor perfomenceMonitor)
        {
            ArgumentNullException.ThrowIfNull(manager);
            ArgumentNullException.ThrowIfNull(renderContext);
            ArgumentNullException.ThrowIfNull(canvas);
            ArgumentNullException.ThrowIfNull(perfomenceMonitor);

            currentModelMatrix = frameState.ModelMatrix;
            currentViewMatrix = frameState.ViewMatrix;
            currentProjectionMatrix = frameState.ProjectionMatrix;

            targetContext = CreateTargetContext(frameState);
            drawingContext = new ReplayDrawingContext(renderContext, targetContext, perfomenceMonitor);
            this.perfomenceMonitor = perfomenceMonitor;

            lineDrawing = new NewSkiaLineDrawing(manager);
            textureDrawing = new DefaultSkiaTextureDrawing(manager);
            batchTextureDrawing = new DefaultSkiaBatchTextureDrawing(manager);
            highlightBatchTextureDrawing = new DefaultSkiaHighlightBatchTextureDrawing(manager);
            circleDrawing = new DefaultSkiaCircleDrawing(manager);
            polygonDrawing = new DefaultSkiaPolygonDrawing(manager);
            stringDrawing = new DefaultSkiaStringDrawing(manager);
            beamDrawing = new DefaultSkiaBeamDrawing(manager);

            if (frameState.CleanColor is { } cleanColor)
                canvas.Clear(new SKColorF(cleanColor.X, cleanColor.Y, cleanColor.Z, cleanColor.W));
        }

        public void Present(IReadOnlyList<DrawCommand> commands)
        {
            ArgumentNullException.ThrowIfNull(commands);

            foreach (var command in commands)
            {
                perfomenceMonitor.OnBeginDrawCommand(command);
                try
                {
                    Present(command);
                }
                finally
                {
                    perfomenceMonitor.OnAfterDrawCommand(command);
                }
            }
        }

        public void Dispose()
        {
            lineDrawing.Dispose();
            textureDrawing.Dispose();
            stringDrawing.Dispose();
        }

        private static DrawingTargetContext CreateTargetContext(DrawCommandListFrameState frameState)
        {
            return new DrawingTargetContext
            {
                Rect = new VisibleRect(new Vector2(frameState.ViewWidth, 0), new Vector2(0, frameState.ViewHeight)),
                ViewWidth = frameState.ViewWidth,
                ViewHeight = frameState.ViewHeight,
                RenderScaleX = frameState.RenderScaleX,
                RenderScaleY = frameState.RenderScaleY,
                ViewMatrix = frameState.ViewMatrix,
                ProjectionMatrix = frameState.ProjectionMatrix
            };
        }

        private void Present(DrawCommand command)
        {
            switch (command)
            {
                case SetCurrentModelMatrixCommand setCurrentModelMatrixCommand:
                    currentModelMatrix = setCurrentModelMatrixCommand.Matrix;
                    break;
                case SetCurrentViewMatrixCommand setCurrentViewMatrixCommand:
                    SetCurrentViewMatrix(setCurrentViewMatrixCommand.Matrix);
                    break;
                case SetCurrentProjectionMatrixCommand setCurrentProjectionMatrixCommand:
                    SetCurrentProjectionMatrix(setCurrentProjectionMatrixCommand.Matrix);
                    break;
                case PushModelMatrixCommand pushModelMatrixCommand:
                    modelMatrixStack.Push(currentModelMatrix);
                    currentModelMatrix = pushModelMatrixCommand.Matrix;
                    break;
                case PushViewMatrixCommand pushViewMatrixCommand:
                    viewMatrixStack.Push(currentViewMatrix);
                    SetCurrentViewMatrix(pushViewMatrixCommand.Matrix);
                    break;
                case PushProjectionMatrixCommand pushProjectionMatrixCommand:
                    projectionMatrixStack.Push(currentProjectionMatrix);
                    SetCurrentProjectionMatrix(pushProjectionMatrixCommand.Matrix);
                    break;
                case PopModelMatrixCommand:
                    currentModelMatrix = PopMatrix(modelMatrixStack, nameof(PopModelMatrixCommand));
                    break;
                case PopViewMatrixCommand:
                    SetCurrentViewMatrix(PopMatrix(viewMatrixStack, nameof(PopViewMatrixCommand)));
                    break;
                case PopProjectionMatrixCommand:
                    SetCurrentProjectionMatrix(PopMatrix(projectionMatrixStack, nameof(PopProjectionMatrixCommand)));
                    break;
                case DrawLinesCommand drawLinesCommand:
                    lineDrawing.PushOverrideModelMatrix(currentModelMatrix);
                    lineDrawing.Draw(drawingContext, drawLinesCommand.Points, drawLinesCommand.LineWidth);
                    lineDrawing.PopOverrideModelMatrix(out _);
                    break;
                case DrawSimpleLinesCommand drawSimpleLinesCommand:
                    lineDrawing.PushOverrideModelMatrix(currentModelMatrix);
                    lineDrawing.Draw(drawingContext, drawSimpleLinesCommand.Points, drawSimpleLinesCommand.LineWidth);
                    lineDrawing.PopOverrideModelMatrix(out _);
                    break;
                case DrawTextureCommand drawTextureCommand:
                    textureDrawing.PushOverrideModelMatrix(currentModelMatrix);
                    textureDrawing.Draw(drawingContext, drawTextureCommand.Texture, EnumerateTextureInstances(drawTextureCommand.Instances));
                    textureDrawing.PopOverrideModelMatrix(out _);
                    break;
                case DrawBatchTextureCommand drawBatchTextureCommand:
                    batchTextureDrawing.PushOverrideModelMatrix(currentModelMatrix);
                    batchTextureDrawing.Draw(drawingContext, drawBatchTextureCommand.Texture, EnumerateTextureInstances(drawBatchTextureCommand.Instances));
                    batchTextureDrawing.PopOverrideModelMatrix(out _);
                    break;
                case DrawHighlightBatchTextureCommand drawHighlightBatchTextureCommand:
                    highlightBatchTextureDrawing.PushOverrideModelMatrix(currentModelMatrix);
                    highlightBatchTextureDrawing.Draw(drawingContext, drawHighlightBatchTextureCommand.Texture, EnumerateTextureInstances(drawHighlightBatchTextureCommand.Instances));
                    highlightBatchTextureDrawing.PopOverrideModelMatrix(out _);
                    break;
                case DrawCirclesCommand drawCirclesCommand:
                    DrawCircles(drawCirclesCommand);
                    break;
                case DrawPolygonCommand drawPolygonCommand:
                    DrawPolygon(drawPolygonCommand);
                    break;
                case DrawStringCommand drawStringCommand:
                    stringDrawing.PushOverrideModelMatrix(currentModelMatrix);
                    stringDrawing.Draw(
                        drawStringCommand.Text,
                        drawStringCommand.Position,
                        drawStringCommand.Scale,
                        drawStringCommand.FontSize,
                        drawStringCommand.Rotate,
                        drawStringCommand.Color,
                        drawStringCommand.Origin,
                        drawStringCommand.Style,
                        drawingContext,
                        drawStringCommand.FontHandle,
                        out _);
                    stringDrawing.PopOverrideModelMatrix(out _);
                    break;
                case DrawBeamCommand drawBeamCommand:
                    beamDrawing.PushOverrideModelMatrix(currentModelMatrix);
                    beamDrawing.Draw(
                        drawingContext,
                        drawBeamCommand.Texture,
                        drawBeamCommand.Width,
                        drawBeamCommand.X,
                        drawBeamCommand.Progress,
                        drawBeamCommand.Color,
                        drawBeamCommand.Rotate,
                        drawBeamCommand.JudgeOffset);
                    beamDrawing.PopOverrideModelMatrix(out _);
                    break;
                default:
                    throw new NotSupportedException($"Unsupported draw command type: {command.GetType().FullName}");
            }
        }

        private void DrawCircles(DrawCirclesCommand command)
        {
            circleDrawing.PushOverrideModelMatrix(currentModelMatrix);
            circleDrawing.Begin(drawingContext);
            foreach (var instance in command.Instances)
                circleDrawing.Post(instance.Point, instance.Color, instance.IsSolid, instance.Radius, instance.HollowLineWidth);
            circleDrawing.End();
            circleDrawing.PopOverrideModelMatrix(out _);
        }

        private void DrawPolygon(DrawPolygonCommand command)
        {
            polygonDrawing.PushOverrideModelMatrix(currentModelMatrix);
            polygonDrawing.Begin(drawingContext, command.Primitive);
            foreach (var vertex in command.Vertices)
                polygonDrawing.PostPoint(vertex.Point, vertex.Color);
            polygonDrawing.End();
            polygonDrawing.PopOverrideModelMatrix(out _);
        }

        private void SetCurrentViewMatrix(Matrix4x4 matrix)
        {
            currentViewMatrix = matrix;
            targetContext.ViewMatrix = matrix;
        }

        private void SetCurrentProjectionMatrix(Matrix4x4 matrix)
        {
            currentProjectionMatrix = matrix;
            targetContext.ProjectionMatrix = matrix;
        }

        private static Matrix4x4 PopMatrix(Stack<Matrix4x4> stack, string commandName)
        {
            if (stack.Count == 0)
                throw new InvalidOperationException($"{commandName} called without a matching push.");

            return stack.Pop();
        }

        private static IEnumerable<(Vector2 size, Vector2 position, float rotation, Vector4 color)> EnumerateTextureInstances(IReadOnlyList<TextureInstance> instances)
        {
            foreach (var instance in instances)
                yield return (instance.Size, instance.Position, instance.Rotation, instance.Color);
        }

        private sealed class ReplayDrawingContext : IDrawingContext
        {
            public ReplayDrawingContext(IRenderContext renderContext, DrawingTargetContext currentDrawingTargetContext, IPerfomenceMonitor perfomenceMonitor)
            {
                RenderContext = renderContext;
                CurrentDrawingTargetContext = currentDrawingTargetContext;
                PerfomenceMonitor = perfomenceMonitor;
            }

            public DrawingTargetContext CurrentDrawingTargetContext { get; }

            public IPerfomenceMonitor PerfomenceMonitor { get; }

            public IRenderContext RenderContext { get; }

            public void Render(IRenderContext context, TimeSpan ts)
            {
            }
        }
    }
}
