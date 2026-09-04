using OngekiFumenEditor.Avalonia.Utils.ObjectPool;
using System;
using System.Collections.Generic;

namespace OngekiFumenEditor.Avalonia.Kernel.Graphics.DrawCommands.DefaultDrawCommands
{
    /// <summary>
    /// Represents a line drawing command.
    /// </summary>
    public sealed class DrawLinesCommand : DrawCommand
    {
        private IPooledList<ILineDrawing.LineVertex> points;

        public DrawLinesCommand()
        {
        }

        internal DrawLinesCommand Initialize(IPooledList<ILineDrawing.LineVertex> points, float lineWidth)
        {
            this.points = points ?? throw new ArgumentNullException(nameof(points));
            LineWidth = lineWidth;
            return this;
        }

        /// <summary>
        /// Gets the line vertices owned by this command.
        /// </summary>
        public IReadOnlyList<ILineDrawing.LineVertex> Points => points is null ? Array.Empty<ILineDrawing.LineVertex>() : points;

        /// <summary>
        /// Gets the line width.
        /// </summary>
        public float LineWidth { get; private set; }

        public override void Dispose()
        {
            points?.Dispose();
            points = null;
            LineWidth = default;
        }

        protected override void ReturnToPoolCore()
        {
            ObjectPool<DrawLinesCommand>.Return(this);
        }
    }
}
