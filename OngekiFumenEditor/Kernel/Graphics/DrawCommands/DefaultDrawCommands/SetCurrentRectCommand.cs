using OngekiFumenEditor.Utils.ObjectPool;

namespace OngekiFumenEditor.Kernel.Graphics.DrawCommands.DefaultDrawCommands
{
    /// <summary>
    /// Replaces the current visible rect while presenting a command list.
    /// </summary>
    public sealed class SetCurrentRectCommand : DrawCommand
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SetCurrentRectCommand"/> class.
        /// </summary>
        public SetCurrentRectCommand()
        {
        }

        internal SetCurrentRectCommand Initialize(VisibleRect rect)
        {
            Rect = rect;
            return this;
        }

        /// <summary>
        /// Gets the rect to use as the current visible rect.
        /// </summary>
        public VisibleRect Rect { get; private set; }

        /// <inheritdoc />
        protected override void ReturnToPoolCore()
        {
            ObjectPool<SetCurrentRectCommand>.Return(this);
        }
    }
}
