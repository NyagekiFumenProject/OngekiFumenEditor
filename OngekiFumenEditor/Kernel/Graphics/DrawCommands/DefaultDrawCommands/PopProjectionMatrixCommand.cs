using OngekiFumenEditor.Core.Utils.ObjectPool;

namespace OngekiFumenEditor.Kernel.Graphics.DrawCommands.DefaultDrawCommands
{
    /// <summary>
    /// Pops the current projection matrix scope while presenting a command list.
    /// </summary>
    public sealed class PopProjectionMatrixCommand : DrawCommand
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PopProjectionMatrixCommand"/> class.
        /// </summary>
        public PopProjectionMatrixCommand()
        {
        }

        internal PopProjectionMatrixCommand Initialize()
        {
            return this;
        }

        /// <inheritdoc />
        protected override void ReturnToPoolCore()
        {
            ObjectPool<PopProjectionMatrixCommand>.Return(this);
        }
    }
}
