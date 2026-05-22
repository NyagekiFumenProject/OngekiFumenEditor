using OngekiFumenEditor.Core.Utils.ObjectPool;
using OngekiFumenEditor.Kernel.Graphics.DrawCommands.DefaultDrawCommands;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace OngekiFumenEditor.Kernel.Graphics.DrawCommands
{
    /// <summary>
    /// Default implementation of the draw command list builder.
    /// </summary>
    public sealed class DrawCommandListBuilder : IDrawCommandListBuilder
    {
        private IPooledList<DrawCommand> commands;
        private IPooledList<Matrix4x4> modelMatrixStack;
        private IPooledList<Matrix4x4> viewMatrixStack;
        private IPooledList<Matrix4x4> projectionMatrixStack;
        private bool disposed;

        private Vector4? cleanColor;
        private float viewWidth;
        private float viewHeight;
        private float renderScaleX;
        private float renderScaleY;
        private Matrix4x4 modelMatrix;
        private Matrix4x4 viewMatrix;
        private Matrix4x4 projectionMatrix;

        /// <summary>
        /// Initializes a new draw command list builder with default frame state.
        /// </summary>
        public DrawCommandListBuilder()
        {
            commands = ObjectPool.GetPooledList<DrawCommand>();
            modelMatrixStack = ObjectPool.GetPooledList<Matrix4x4>();
            viewMatrixStack = ObjectPool.GetPooledList<Matrix4x4>();
            projectionMatrixStack = ObjectPool.GetPooledList<Matrix4x4>();
            ResetState();
        }

        /// <inheritdoc />
        public void SetCleanColor(Vector4? color)
        {
            ThrowIfDisposed();
            cleanColor = color;
        }

        /// <inheritdoc />
        public void SetViewport(float viewWidth, float viewHeight, float renderScaleX = 1, float renderScaleY = 1)
        {
            ThrowIfDisposed();

            if (viewWidth < 0)
                throw new ArgumentOutOfRangeException(nameof(viewWidth));
            if (viewHeight < 0)
                throw new ArgumentOutOfRangeException(nameof(viewHeight));
            if (renderScaleX <= 0)
                throw new ArgumentOutOfRangeException(nameof(renderScaleX));
            if (renderScaleY <= 0)
                throw new ArgumentOutOfRangeException(nameof(renderScaleY));

            this.viewWidth = viewWidth;
            this.viewHeight = viewHeight;
            this.renderScaleX = renderScaleX;
            this.renderScaleY = renderScaleY;
        }

        /// <inheritdoc />
        public void SetCurrentModelMatrix(Matrix4x4 matrix)
        {
            ThrowIfDisposed();
            modelMatrix = matrix;
            commands.Add(RentCommand<SetCurrentModelMatrixCommand>().Initialize(matrix));
        }

        /// <inheritdoc />
        public void SetCurrentViewMatrix(Matrix4x4 matrix)
        {
            ThrowIfDisposed();
            viewMatrix = matrix;
            commands.Add(RentCommand<SetCurrentViewMatrixCommand>().Initialize(matrix));
        }

        /// <inheritdoc />
        public void SetCurrentProjectionMatrix(Matrix4x4 matrix)
        {
            ThrowIfDisposed();
            projectionMatrix = matrix;
            commands.Add(RentCommand<SetCurrentProjectionMatrixCommand>().Initialize(matrix));
        }

        /// <inheritdoc />
        public void PushModelMatrix(Matrix4x4 matrix)
        {
            ThrowIfDisposed();
            modelMatrixStack.Add(modelMatrix);
            modelMatrix = matrix;
            commands.Add(RentCommand<PushModelMatrixCommand>().Initialize(matrix));
        }

        /// <inheritdoc />
        public void PushViewMatrix(Matrix4x4 matrix)
        {
            ThrowIfDisposed();
            viewMatrixStack.Add(viewMatrix);
            viewMatrix = matrix;
            commands.Add(RentCommand<PushViewMatrixCommand>().Initialize(matrix));
        }

        /// <inheritdoc />
        public void PushProjectionMatrix(Matrix4x4 matrix)
        {
            ThrowIfDisposed();
            projectionMatrixStack.Add(projectionMatrix);
            projectionMatrix = matrix;
            commands.Add(RentCommand<PushProjectionMatrixCommand>().Initialize(matrix));
        }

        /// <inheritdoc />
        public void PopModelMatrix()
        {
            ThrowIfDisposed();
            modelMatrix = PopMatrix(modelMatrixStack, nameof(PopModelMatrix));
            commands.Add(RentCommand<PopModelMatrixCommand>().Initialize());
        }

        /// <inheritdoc />
        public void PopViewMatrix()
        {
            ThrowIfDisposed();
            viewMatrix = PopMatrix(viewMatrixStack, nameof(PopViewMatrix));
            commands.Add(RentCommand<PopViewMatrixCommand>().Initialize());
        }

        /// <inheritdoc />
        public void PopProjectionMatrix()
        {
            ThrowIfDisposed();
            projectionMatrix = PopMatrix(projectionMatrixStack, nameof(PopProjectionMatrix));
            commands.Add(RentCommand<PopProjectionMatrixCommand>().Initialize());
        }

        /// <inheritdoc />
        public void DrawLines(IEnumerable<ILineDrawing.LineVertex> points, float lineWidth)
        {
            ThrowIfDisposed();
            ValidateLineWidth(lineWidth);

            var pointList = CopyToPooledList(points, nameof(points));
            if (pointList.Count == 0)
            {
                pointList.Dispose();
                return;
            }

            commands.Add(RentCommand<DrawLinesCommand>().Initialize(pointList, lineWidth));
        }

        /// <inheritdoc />
        public void DrawSimpleLines(IEnumerable<ILineDrawing.LineVertex> points, float lineWidth)
        {
            ThrowIfDisposed();
            ValidateLineWidth(lineWidth);

            var pointList = CopyToPooledList(points, nameof(points));
            if (pointList.Count == 0)
            {
                pointList.Dispose();
                return;
            }

            commands.Add(RentCommand<DrawSimpleLinesCommand>().Initialize(pointList, lineWidth));
        }

        /// <inheritdoc />
        public void DrawTexture(IImage texture, IEnumerable<TextureInstance> instances)
        {
            ThrowIfDisposed();
            ValidateTexture(texture);
            AddTextureCommand<DrawTextureCommand>(texture, instances, static (command, tex, list) => command.Initialize(tex, list));
        }

        /// <inheritdoc />
        public void DrawBatchTexture(IImage texture, IEnumerable<TextureInstance> instances)
        {
            ThrowIfDisposed();
            ValidateTexture(texture);
            AddTextureCommand<DrawBatchTextureCommand>(texture, instances, static (command, tex, list) => command.Initialize(tex, list));
        }

        /// <inheritdoc />
        public void DrawHighlightBatchTexture(IImage texture, IEnumerable<TextureInstance> instances)
        {
            ThrowIfDisposed();
            ValidateTexture(texture);
            AddTextureCommand<DrawHighlightBatchTextureCommand>(texture, instances, static (command, tex, list) => command.Initialize(tex, list));
        }

        /// <inheritdoc />
        public void DrawCircle(Vector2 point, Vector4 color, bool isSolid, float radius, float hollowLineWidth = 0)
        {
            ThrowIfDisposed();
            ValidateCircle(radius, hollowLineWidth);

            var instances = ObjectPool.GetPooledList<CircleInstance>();
            instances.Add(new CircleInstance(point, color, isSolid, radius, hollowLineWidth));
            commands.Add(RentCommand<DrawCirclesCommand>().Initialize(instances));
        }

        /// <inheritdoc />
        public void DrawCircles(IEnumerable<CircleInstance> instances)
        {
            ThrowIfDisposed();

            var instanceList = CopyToPooledList(instances, nameof(instances));
            if (instanceList.Count == 0)
            {
                instanceList.Dispose();
                return;
            }

            foreach (var instance in instanceList)
                ValidateCircle(instance.Radius, instance.HollowLineWidth);

            commands.Add(RentCommand<DrawCirclesCommand>().Initialize(instanceList));
        }

        /// <inheritdoc />
        public void DrawPolygon(Primitive primitive, IEnumerable<PolygonVertex> vertices)
        {
            ThrowIfDisposed();

            var vertexList = CopyToPooledList(vertices, nameof(vertices));
            if (vertexList.Count == 0)
            {
                vertexList.Dispose();
                return;
            }

            commands.Add(RentCommand<DrawPolygonCommand>().Initialize(primitive, vertexList));
        }

        /// <inheritdoc />
        public void DrawString(string text, Vector2 pos, Vector2 scale, int fontSize, float rotate, Vector4 color, Vector2 origin, IStringDrawing.StringStyle style, IStringDrawing.IFontHandle handle)
        {
            ThrowIfDisposed();

            if (text is null)
                throw new ArgumentNullException(nameof(text));
            if (handle is null)
                throw new ArgumentNullException(nameof(handle));
            if (fontSize <= 0)
                throw new ArgumentOutOfRangeException(nameof(fontSize));

            commands.Add(RentCommand<DrawStringCommand>().Initialize(text, pos, scale, fontSize, rotate, color, origin, style, handle));
        }

        /// <inheritdoc />
        public void DrawBeam(IImage texture, int width, float x, float progress, Vector4 color, float rotate, float judgeOffset)
        {
            ThrowIfDisposed();
            ValidateTexture(texture);

            if (width <= 0)
                throw new ArgumentOutOfRangeException(nameof(width));

            commands.Add(RentCommand<DrawBeamCommand>().Initialize(texture, width, x, progress, color, rotate, judgeOffset));
        }

        /// <inheritdoc />
        public void DrawDrawCommandList(IEnumerable<DrawCommand> drawCommands)
        {
            ThrowIfDisposed();

            if (drawCommands is null)
                throw new ArgumentNullException(nameof(drawCommands));

            foreach (var command in drawCommands)
                AddCopiedDrawCommand(command);
        }

        /// <inheritdoc />
        public DrawCommandList GetDrawCommandList()
        {
            ThrowIfDisposed();

            var listCommands = commands;
            var frameState = new DrawCommandListFrameState(cleanColor, viewWidth, viewHeight, renderScaleX, renderScaleY, modelMatrix, viewMatrix, projectionMatrix);
            commands = ObjectPool.GetPooledList<DrawCommand>();
            ResetState();

            return new DrawCommandList(listCommands, frameState);
        }

        /// <inheritdoc />
        public void Clear()
        {
            ThrowIfDisposed();

            ReleaseCommands(commands);

            commands.Clear();
            modelMatrixStack.Clear();
            viewMatrixStack.Clear();
            projectionMatrixStack.Clear();
            ResetState();
        }

        /// <summary>
        /// Releases pending commands and pooled collections owned by the builder.
        /// </summary>
        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;

            if (commands is not null)
            {
                ReleaseCommands(commands);
                commands.Dispose();
                commands = null;
            }

            modelMatrixStack?.Dispose();
            modelMatrixStack = null;
            viewMatrixStack?.Dispose();
            viewMatrixStack = null;
            projectionMatrixStack?.Dispose();
            projectionMatrixStack = null;
        }

        private void AddTextureCommand<TCommand>(IImage texture, IEnumerable<TextureInstance> instances, Func<TCommand, IImage, IPooledList<TextureInstance>, TCommand> initialize)
            where TCommand : DrawCommand, new()
        {
            var instanceList = CopyToPooledList(instances, nameof(instances));
            if (instanceList.Count == 0)
            {
                instanceList.Dispose();
                return;
            }

            commands.Add(initialize(RentCommand<TCommand>(), texture, instanceList));
        }

        private void ResetState()
        {
            cleanColor = new Vector4(0, 0, 0, 1);
            viewWidth = 0;
            viewHeight = 0;
            renderScaleX = 1;
            renderScaleY = 1;
            modelMatrix = Matrix4x4.Identity;
            viewMatrix = Matrix4x4.Identity;
            projectionMatrix = Matrix4x4.Identity;
            modelMatrixStack?.Clear();
            viewMatrixStack?.Clear();
            projectionMatrixStack?.Clear();
        }

        private void ThrowIfDisposed()
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(DrawCommandListBuilder));
        }

        private static void ValidateLineWidth(float lineWidth)
        {
            if (lineWidth <= 0)
                throw new ArgumentOutOfRangeException(nameof(lineWidth));
        }

        private static void ValidateTexture(IImage texture)
        {
            if (texture is null)
                throw new ArgumentNullException(nameof(texture));
        }

        private static void ValidateCircle(float radius, float hollowLineWidth)
        {
            if (radius < 0)
                throw new ArgumentOutOfRangeException(nameof(radius));
            if (hollowLineWidth < 0)
                throw new ArgumentOutOfRangeException(nameof(hollowLineWidth));
        }

        private static Matrix4x4 PopMatrix(IPooledList<Matrix4x4> stack, string operationName)
        {
            if (stack.Count == 0)
                throw new InvalidOperationException($"{operationName} called without a matching push.");

            var index = stack.Count - 1;
            var matrix = stack[index];
            stack.RemoveAt(index);
            return matrix;
        }

        private static IPooledList<T> CopyToPooledList<T>(IEnumerable<T> source, string paramName)
        {
            if (source is null)
                throw new ArgumentNullException(paramName);

            var list = ObjectPool.GetPooledList<T>();
            list.AddRange(source);
            return list;
        }

        private static TCommand RentCommand<TCommand>()
            where TCommand : DrawCommand, new()
        {
            return ObjectPool.Get<TCommand>();
        }

        private void AddCopiedDrawCommand(DrawCommand command)
        {
            if (command is null)
                throw new ArgumentNullException(nameof(command));

            switch (command)
            {
                case SetCurrentModelMatrixCommand setCurrentModelMatrixCommand:
                    SetCurrentModelMatrix(setCurrentModelMatrixCommand.Matrix);
                    return;
                case SetCurrentViewMatrixCommand setCurrentViewMatrixCommand:
                    SetCurrentViewMatrix(setCurrentViewMatrixCommand.Matrix);
                    return;
                case SetCurrentProjectionMatrixCommand setCurrentProjectionMatrixCommand:
                    SetCurrentProjectionMatrix(setCurrentProjectionMatrixCommand.Matrix);
                    return;
                case PushModelMatrixCommand pushModelMatrixCommand:
                    PushModelMatrix(pushModelMatrixCommand.Matrix);
                    return;
                case PushViewMatrixCommand pushViewMatrixCommand:
                    PushViewMatrix(pushViewMatrixCommand.Matrix);
                    return;
                case PushProjectionMatrixCommand pushProjectionMatrixCommand:
                    PushProjectionMatrix(pushProjectionMatrixCommand.Matrix);
                    return;
                case PopModelMatrixCommand:
                    PopModelMatrix();
                    return;
                case PopViewMatrixCommand:
                    PopViewMatrix();
                    return;
                case PopProjectionMatrixCommand:
                    PopProjectionMatrix();
                    return;
                case DrawLinesCommand drawLinesCommand:
                    DrawLines(drawLinesCommand.Points, drawLinesCommand.LineWidth);
                    return;
                case DrawSimpleLinesCommand drawSimpleLinesCommand:
                    DrawSimpleLines(drawSimpleLinesCommand.Points, drawSimpleLinesCommand.LineWidth);
                    return;
                case DrawTextureCommand drawTextureCommand:
                    DrawTexture(drawTextureCommand.Texture, drawTextureCommand.Instances);
                    return;
                case DrawBatchTextureCommand drawBatchTextureCommand:
                    DrawBatchTexture(drawBatchTextureCommand.Texture, drawBatchTextureCommand.Instances);
                    return;
                case DrawHighlightBatchTextureCommand drawHighlightBatchTextureCommand:
                    DrawHighlightBatchTexture(drawHighlightBatchTextureCommand.Texture, drawHighlightBatchTextureCommand.Instances);
                    return;
                case DrawCirclesCommand drawCirclesCommand:
                    DrawCircles(drawCirclesCommand.Instances);
                    return;
                case DrawPolygonCommand drawPolygonCommand:
                    DrawPolygon(drawPolygonCommand.Primitive, drawPolygonCommand.Vertices);
                    return;
                case DrawStringCommand drawStringCommand:
                    DrawString(drawStringCommand.Text, drawStringCommand.Position, drawStringCommand.Scale, drawStringCommand.FontSize, drawStringCommand.Rotate, drawStringCommand.Color, drawStringCommand.Origin, drawStringCommand.Style, drawStringCommand.FontHandle);
                    return;
                case DrawBeamCommand drawBeamCommand:
                    DrawBeam(drawBeamCommand.Texture, drawBeamCommand.Width, drawBeamCommand.X, drawBeamCommand.Progress, drawBeamCommand.Color, drawBeamCommand.Rotate, drawBeamCommand.JudgeOffset);
                    return;
                default:
                    throw new NotSupportedException($"Unsupported draw command type: {command.GetType().FullName}");
            }
        }

        private static void ReleaseCommands(IPooledList<DrawCommand> commandList)
        {
            foreach (var command in commandList)
                command?.DisposeAndReturnSelf();
        }
    }
}
