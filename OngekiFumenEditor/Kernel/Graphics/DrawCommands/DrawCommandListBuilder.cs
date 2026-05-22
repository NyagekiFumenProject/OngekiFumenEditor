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
            commands.Add(new SetCurrentModelMatrixCommand(matrix));
        }

        /// <inheritdoc />
        public void SetCurrentViewMatrix(Matrix4x4 matrix)
        {
            ThrowIfDisposed();
            viewMatrix = matrix;
            commands.Add(new SetCurrentViewMatrixCommand(matrix));
        }

        /// <inheritdoc />
        public void SetCurrentProjectionMatrix(Matrix4x4 matrix)
        {
            ThrowIfDisposed();
            projectionMatrix = matrix;
            commands.Add(new SetCurrentProjectionMatrixCommand(matrix));
        }

        /// <inheritdoc />
        public void PushModelMatrix(Matrix4x4 matrix)
        {
            ThrowIfDisposed();
            modelMatrixStack.Add(modelMatrix);
            modelMatrix = matrix;
            commands.Add(new PushModelMatrixCommand(matrix));
        }

        /// <inheritdoc />
        public void PushViewMatrix(Matrix4x4 matrix)
        {
            ThrowIfDisposed();
            viewMatrixStack.Add(viewMatrix);
            viewMatrix = matrix;
            commands.Add(new PushViewMatrixCommand(matrix));
        }

        /// <inheritdoc />
        public void PushProjectionMatrix(Matrix4x4 matrix)
        {
            ThrowIfDisposed();
            projectionMatrixStack.Add(projectionMatrix);
            projectionMatrix = matrix;
            commands.Add(new PushProjectionMatrixCommand(matrix));
        }

        /// <inheritdoc />
        public void PopModelMatrix()
        {
            ThrowIfDisposed();
            modelMatrix = PopMatrix(modelMatrixStack, nameof(PopModelMatrix));
            commands.Add(new PopModelMatrixCommand());
        }

        /// <inheritdoc />
        public void PopViewMatrix()
        {
            ThrowIfDisposed();
            viewMatrix = PopMatrix(viewMatrixStack, nameof(PopViewMatrix));
            commands.Add(new PopViewMatrixCommand());
        }

        /// <inheritdoc />
        public void PopProjectionMatrix()
        {
            ThrowIfDisposed();
            projectionMatrix = PopMatrix(projectionMatrixStack, nameof(PopProjectionMatrix));
            commands.Add(new PopProjectionMatrixCommand());
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

            commands.Add(new DrawLinesCommand(pointList, lineWidth));
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

            commands.Add(new DrawSimpleLinesCommand(pointList, lineWidth));
        }

        /// <inheritdoc />
        public void DrawTexture(IImage texture, IEnumerable<TextureInstance> instances)
        {
            ThrowIfDisposed();
            ValidateTexture(texture);
            AddTextureCommand(texture, instances, static (tex, list) => new DrawTextureCommand(tex, list));
        }

        /// <inheritdoc />
        public void DrawBatchTexture(IImage texture, IEnumerable<TextureInstance> instances)
        {
            ThrowIfDisposed();
            ValidateTexture(texture);
            AddTextureCommand(texture, instances, static (tex, list) => new DrawBatchTextureCommand(tex, list));
        }

        /// <inheritdoc />
        public void DrawHighlightBatchTexture(IImage texture, IEnumerable<TextureInstance> instances)
        {
            ThrowIfDisposed();
            ValidateTexture(texture);
            AddTextureCommand(texture, instances, static (tex, list) => new DrawHighlightBatchTextureCommand(tex, list));
        }

        /// <inheritdoc />
        public void DrawCircle(Vector2 point, Vector4 color, bool isSolid, float radius, float hollowLineWidth = 0)
        {
            ThrowIfDisposed();
            ValidateCircle(radius, hollowLineWidth);

            var instances = ObjectPool.GetPooledList<CircleInstance>();
            instances.Add(new CircleInstance(point, color, isSolid, radius, hollowLineWidth));
            commands.Add(new DrawCirclesCommand(instances));
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

            commands.Add(new DrawCirclesCommand(instanceList));
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

            commands.Add(new DrawPolygonCommand(primitive, vertexList));
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

            commands.Add(new DrawStringCommand(text, pos, scale, fontSize, rotate, color, origin, style, handle));
        }

        /// <inheritdoc />
        public void DrawBeam(IImage texture, int width, float x, float progress, Vector4 color, float rotate, float judgeOffset)
        {
            ThrowIfDisposed();
            ValidateTexture(texture);

            if (width <= 0)
                throw new ArgumentOutOfRangeException(nameof(width));

            commands.Add(new DrawBeamCommand(texture, width, x, progress, color, rotate, judgeOffset));
        }

        /// <inheritdoc />
        public void DrawStaticVBO(IStaticVBODrawing.IVBOHandle vbo)
        {
            ThrowIfDisposed();

            if (vbo is null)
                throw new ArgumentNullException(nameof(vbo));

            commands.Add(new DrawStaticVBOCommand(vbo));
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

            foreach (var command in commands)
                command?.Dispose();

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
                foreach (var command in commands)
                    command?.Dispose();
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

        private void AddTextureCommand(IImage texture, IEnumerable<TextureInstance> instances, Func<IImage, IPooledList<TextureInstance>, DrawCommand> factory)
        {
            var instanceList = CopyToPooledList(instances, nameof(instances));
            if (instanceList.Count == 0)
            {
                instanceList.Dispose();
                return;
            }

            commands.Add(factory(texture, instanceList));
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
    }
}
