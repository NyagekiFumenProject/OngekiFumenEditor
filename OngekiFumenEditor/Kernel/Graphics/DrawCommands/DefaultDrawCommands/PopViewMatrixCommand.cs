using OngekiFumenEditor.Utils.ObjectPool;

namespace OngekiFumenEditor.Kernel.Graphics.DrawCommands.DefaultDrawCommands
{
    /// <summary>
    /// Pops the current view matrix scope while presenting a command list.
    /// </summary>
    public sealed class PopViewMatrixCommand : DrawCommand
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PopViewMatrixCommand"/> class.
        /// </summary>
        public PopViewMatrixCommand()
        {
        }

        internal PopViewMatrixCommand Initialize()
        {
            return this;
        }

        /// <inheritdoc />
        protected override void ReturnToPoolCore()
        {
            ObjectPool<PopViewMatrixCommand>.Return(this);
        }
    }
}
