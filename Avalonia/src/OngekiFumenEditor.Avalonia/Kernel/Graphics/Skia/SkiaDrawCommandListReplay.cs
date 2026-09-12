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
        private readonly SKCanvas canvas;

        private readonly Stack<Matrix4> modelMatrixStack = new();
        private readonly Stack<Matrix4> viewMatrixStack = new();
        private readonly Stack<Matrix4> projectionMatrixStack = new();

        private Matrix4 currentModelMatrix;
        private Matrix4 currentViewMatrix;
        private Matrix4 currentProjectionMatrix;

        public SkiaDrawCommandListReplay(DefaultSkiaDrawingManagerImpl manager, IRenderContext renderContext, SKCanvas canvas)
        {
            ArgumentNullException.ThrowIfNull(manager);
            ArgumentNullException.ThrowIfNull(renderContext);
            ArgumentNullException.ThrowIfNull(canvas);

            this.renderContext = renderContext;
            this.canvas = canvas;

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

        public void Present(IReadOnlyList<DrawCommand> commands, DrawCommandListFrameState frameState)
        {
            ArgumentNullException.ThrowIfNull(commands);

            if (frameState.CleanColor is { } cleanColor)
                canvas.Clear(new SKColorF(cleanColor.X, cleanColor.Y, cleanColor.Z, cleanColor.W));

            currentModelMatrix = frameState.ModelMatrix;
            currentViewMatrix = frameState.ViewMatrix;
            currentProjectionMatrix = frameState.ProjectionMatrix;

            targetContext.ViewWidth = frameState.ViewWidth;
            targetContext.ViewHeight = frameState.ViewHeight;
            targetContext.ViewMatrix = currentViewMatrix;
            targetContext.ProjectionMatrix = currentProjectionMatrix;
            targetContext.ViewRelativeRect = new VisibleRect(new Vector2(frameState.ViewWidth, 0), new Vector2(0, frameState.ViewHeight));

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

        public void Dispose()
        {
            lineDrawing.Dispose();
            stringDrawing.Dispose();
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
