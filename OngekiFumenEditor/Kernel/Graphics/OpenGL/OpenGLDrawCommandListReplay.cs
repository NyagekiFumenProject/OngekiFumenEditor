using OngekiFumenEditor.Kernel.Graphics.DrawCommands;
using OngekiFumenEditor.Kernel.Graphics.DrawCommands.DefaultDrawCommands;
using OngekiFumenEditor.Kernel.Graphics.OpenGL.Drawing.BeamDrawing;
using OngekiFumenEditor.Kernel.Graphics.OpenGL.Drawing.CircleDrawing;
using OngekiFumenEditor.Kernel.Graphics.OpenGL.Drawing.LineDrawing;
using OngekiFumenEditor.Kernel.Graphics.OpenGL.Drawing.PolygonDrawing;
using OngekiFumenEditor.Kernel.Graphics.OpenGL.Drawing.StringDrawing;
using OngekiFumenEditor.Kernel.Graphics.OpenGL.Drawing.TextureDrawing;
using OngekiFumenEditor.Kernel.Graphics.Performence;
using OngekiFumenEditor.Modules.FumenVisualEditor.Graphics.Drawing;
using OpenTK.Graphics.OpenGL;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace OngekiFumenEditor.Kernel.Graphics.OpenGL
{
    internal static class OpenGLDrawCommandListReplay
    {
        private static readonly object replayGate = new();
        private static readonly ReplayDrawingContext drawingContext = new();
        private static DefaultLineDrawing lineDrawing;
        private static DefaultInstancedLineDrawing simpleLineDrawing;
        private static DefaultTextureDrawing textureDrawing;
        private static DefaultBatchTextureDrawing batchTextureDrawing;
        private static DefaultHighlightBatchTextureDrawing highlightBatchTextureDrawing;
        private static DefaultInstancedCircleDrawing circleDrawing;
        private static DefaultPolygonDrawing polygonDrawing;
        private static DefaultStringDrawing stringDrawing;
        private static DefaultBeamDrawing beamDrawing;

        private static readonly Stack<Matrix4x4> modelMatrixStack = new();
        private static readonly Stack<Matrix4x4> viewMatrixStack = new();
        private static readonly Stack<Matrix4x4> projectionMatrixStack = new();

        private static Matrix4x4 currentModelMatrix;
        private static Matrix4x4 currentViewMatrix;
        private static Matrix4x4 currentProjectionMatrix;

        public static void Present(DefaultOpenGLRenderManagerImpl manager, IRenderContext renderContext, DrawCommandList drawCommandList)
        {
            ArgumentNullException.ThrowIfNull(manager);
            ArgumentNullException.ThrowIfNull(renderContext);
            ArgumentNullException.ThrowIfNull(drawCommandList);

            lock (replayGate)
            {
                EnsureInitialized(manager);
                Reset(renderContext, drawCommandList.FrameState);
                Present(drawCommandList.Commands);
            }
        }

        private static void EnsureInitialized(DefaultOpenGLRenderManagerImpl manager)
        {
            if (lineDrawing is not null)
                return;

            lineDrawing = new(manager);
            simpleLineDrawing = new(manager);
            textureDrawing = new(manager);
            batchTextureDrawing = new(manager);
            highlightBatchTextureDrawing = new(manager);
            circleDrawing = new(manager);
            polygonDrawing = new(manager);
            stringDrawing = new(manager);
            beamDrawing = new(manager);
        }

        public static void Dispose()
        {
            lock (replayGate)
            {
                lineDrawing?.Dispose();
                simpleLineDrawing?.Dispose();
                textureDrawing?.Dispose();
                batchTextureDrawing?.Dispose();
                highlightBatchTextureDrawing?.Dispose();
                polygonDrawing?.Dispose();
                stringDrawing?.Dispose();
                beamDrawing?.Dispose();

                lineDrawing = null;
                simpleLineDrawing = null;
                textureDrawing = null;
                batchTextureDrawing = null;
                highlightBatchTextureDrawing = null;
                circleDrawing = null;
                polygonDrawing = null;
                stringDrawing = null;
                beamDrawing = null;
            }
        }

        private static void Reset(IRenderContext renderContext, DrawCommandListFrameState frameState)
        {
            currentModelMatrix = frameState.ModelMatrix;
            currentViewMatrix = frameState.ViewMatrix;
            currentProjectionMatrix = frameState.ProjectionMatrix;

            modelMatrixStack.Clear();
            viewMatrixStack.Clear();
            projectionMatrixStack.Clear();

            drawingContext.Reset(renderContext, CreateTargetContext(frameState));

            var renderViewWidth = (int)(frameState.ViewWidth * frameState.RenderScaleX);
            var renderViewHeight = (int)(frameState.ViewHeight * frameState.RenderScaleY);
            GL.Viewport(0, 0, renderViewWidth, renderViewHeight);

            if (frameState.CleanColor is { } cleanColor)
            {
                GL.ClearColor(cleanColor.X, cleanColor.Y, cleanColor.Z, cleanColor.W);
                GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
            }
        }

        private static void Present(IReadOnlyList<DrawCommand> commands)
        {
            ArgumentNullException.ThrowIfNull(commands);

            foreach (var command in commands)
            {
                var perfomenceMonitor = PerfomenceMonitor;
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

        private static IPerfomenceMonitor PerfomenceMonitor => drawingContext.RenderContext?.PerfomenceMonitor ?? DummyPerformenceMonitor.Instance;

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

        private static void Present(DrawCommand command)
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
                case SetCurrentRectCommand setCurrentRectCommand:
                    drawingContext.CurrentDrawingTargetContext.Rect = setCurrentRectCommand.Rect;
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
                    try
                    {
                        lineDrawing.Draw(drawingContext, drawLinesCommand.Points, drawLinesCommand.LineWidth);
                    }
                    finally
                    {
                        lineDrawing.PopOverrideModelMatrix(out _);
                    }
                    break;
                case DrawSimpleLinesCommand drawSimpleLinesCommand:
                    simpleLineDrawing.PushOverrideModelMatrix(currentModelMatrix);
                    try
                    {
                        simpleLineDrawing.Draw(drawingContext, drawSimpleLinesCommand.Points, drawSimpleLinesCommand.LineWidth);
                    }
                    finally
                    {
                        simpleLineDrawing.PopOverrideModelMatrix(out _);
                    }
                    break;
                case DrawTextureCommand drawTextureCommand:
                    textureDrawing.PushOverrideModelMatrix(currentModelMatrix);
                    try
                    {
                        textureDrawing.Draw(drawingContext, drawTextureCommand.Texture, EnumerateTextureInstances(drawTextureCommand.Instances));
                    }
                    finally
                    {
                        textureDrawing.PopOverrideModelMatrix(out _);
                    }
                    break;
                case DrawBatchTextureCommand drawBatchTextureCommand:
                    batchTextureDrawing.PushOverrideModelMatrix(currentModelMatrix);
                    try
                    {
                        batchTextureDrawing.Draw(drawingContext, drawBatchTextureCommand.Texture, EnumerateTextureInstances(drawBatchTextureCommand.Instances));
                    }
                    finally
                    {
                        batchTextureDrawing.PopOverrideModelMatrix(out _);
                    }
                    break;
                case DrawHighlightBatchTextureCommand drawHighlightBatchTextureCommand:
                    highlightBatchTextureDrawing.PushOverrideModelMatrix(currentModelMatrix);
                    try
                    {
                        highlightBatchTextureDrawing.Draw(drawingContext, drawHighlightBatchTextureCommand.Texture, EnumerateTextureInstances(drawHighlightBatchTextureCommand.Instances));
                    }
                    finally
                    {
                        highlightBatchTextureDrawing.PopOverrideModelMatrix(out _);
                    }
                    break;
                case DrawCirclesCommand drawCirclesCommand:
                    DrawCircles(drawCirclesCommand);
                    break;
                case DrawPolygonCommand drawPolygonCommand:
                    DrawPolygon(drawPolygonCommand);
                    break;
                case DrawStringCommand drawStringCommand:
                    stringDrawing.PushOverrideModelMatrix(currentModelMatrix);
                    try
                    {
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
                    }
                    finally
                    {
                        stringDrawing.PopOverrideModelMatrix(out _);
                    }
                    break;
                case DrawBeamCommand drawBeamCommand:
                    beamDrawing.PushOverrideModelMatrix(currentModelMatrix);
                    try
                    {
                        beamDrawing.Draw(
                            drawingContext,
                            drawBeamCommand.Texture,
                            drawBeamCommand.Width,
                            drawBeamCommand.X,
                            drawBeamCommand.Progress,
                            drawBeamCommand.Color,
                            drawBeamCommand.Rotate,
                            drawBeamCommand.JudgeOffset);
                    }
                    finally
                    {
                        beamDrawing.PopOverrideModelMatrix(out _);
                    }
                    break;
                default:
                    throw new NotSupportedException($"Unsupported draw command type: {command.GetType().FullName}");
            }
        }

        private static void DrawCircles(DrawCirclesCommand command)
        {
            circleDrawing.PushOverrideModelMatrix(currentModelMatrix);
            try
            {
                circleDrawing.Begin(drawingContext);
                foreach (var instance in command.Instances)
                    circleDrawing.Post(instance.Point, instance.Color, instance.IsSolid, instance.Radius, instance.HollowLineWidth);
                circleDrawing.End();
            }
            finally
            {
                circleDrawing.PopOverrideModelMatrix(out _);
            }
        }

        private static void DrawPolygon(DrawPolygonCommand command)
        {
            polygonDrawing.PushOverrideModelMatrix(currentModelMatrix);
            try
            {
                polygonDrawing.Begin(drawingContext, command.Primitive);
                foreach (var vertex in command.Vertices)
                    polygonDrawing.PostPoint(vertex.Point, vertex.Color);
                polygonDrawing.End();
            }
            finally
            {
                polygonDrawing.PopOverrideModelMatrix(out _);
            }
        }

        private static void SetCurrentViewMatrix(Matrix4x4 matrix)
        {
            currentViewMatrix = matrix;
            drawingContext.CurrentDrawingTargetContext.ViewMatrix = matrix;
        }

        private static void SetCurrentProjectionMatrix(Matrix4x4 matrix)
        {
            currentProjectionMatrix = matrix;
            drawingContext.CurrentDrawingTargetContext.ProjectionMatrix = matrix;
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
            public DrawingTargetContext CurrentDrawingTargetContext { get; private set; }

            public IPerfomenceMonitor PerfomenceMonitor => RenderContext?.PerfomenceMonitor ?? DummyPerformenceMonitor.Instance;

            public IRenderContext RenderContext { get; private set; }

            public void Reset(IRenderContext renderContext, DrawingTargetContext currentDrawingTargetContext)
            {
                RenderContext = renderContext;
                CurrentDrawingTargetContext = currentDrawingTargetContext;
            }

            public void Render(IRenderContext context, TimeSpan ts)
            {
            }
        }
    }
}
