using OngekiFumenEditor.Avalonia.Utils.ObjectPool;
using System;
using System.Collections.Generic;

namespace OngekiFumenEditor.Avalonia.Kernel.Graphics.DrawCommands.DefaultDrawCommands
{
    /// <summary>
    /// Represents a polygon drawing command.
    /// </summary>
    public sealed class DrawPolygonCommand : DrawCommand
    {
        private IPooledList<PolygonVertex> vertices;

        public DrawPolygonCommand()
        {
        }

        internal DrawPolygonCommand Initialize(Primitive primitive, IPooledList<PolygonVertex> vertices)
        {
            Primitive = primitive;
            this.vertices = vertices ?? throw new ArgumentNullException(nameof(vertices));
            return this;
        }

        /// <summary>
        /// Gets the primitive topology used for the polygon.
        /// </summary>
        public Primitive Primitive { get; private set; }

        /// <summary>
        /// Gets the polygon vertices owned by this command.
        /// </summary>
        public IReadOnlyList<PolygonVertex> Vertices => vertices is null ? Array.Empty<PolygonVertex>() : vertices;

        public override void Dispose()
        {
            vertices?.Dispose();
            vertices = null;
            Primitive = default;
        }

        protected override void ReturnToPoolCore()
        {
            ObjectPool<DrawPolygonCommand>.Return(this);
        }
    }
}