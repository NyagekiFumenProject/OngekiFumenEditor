using OngekiFumenEditor.Core.Utils.ObjectPool;
using System;
using System.Collections.Generic;

namespace OngekiFumenEditor.Kernel.Graphics.DrawCommands.DefaultDrawCommands
{
    /// <summary>
    /// Represents a simple line drawing command.
    /// </summary>
    public sealed record DrawSimpleLinesCommand : DrawCommand
    {
        private IPooledList<ILineDrawing.LineVertex> points;

        /// <summary>
        /// Initializes a simple line drawing command.
        /// </summary>
        public DrawSimpleLinesCommand(IPooledList<ILineDrawing.LineVertex> points, float lineWidth)
        {
            this.points = points ?? throw new ArgumentNullException(nameof(points));
            LineWidth = lineWidth;
        }

        /// <summary>
        /// Gets the line vertices owned by this command.
        /// </summary>
        public IReadOnlyList<ILineDrawing.LineVertex> Points => points is null ? Array.Empty<ILineDrawing.LineVertex>() : points;

        /// <summary>
        /// Gets the line width.
        /// </summary>
        public float LineWidth { get; }

        /// <summary>
        /// Releases the pooled point list owned by this command.
        /// </summary>
        public override void Dispose()
        {
            points?.Dispose();
            points = null;
        }
    }
}
