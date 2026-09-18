using OngekiFumenEditor.Utils.ObjectPool;

namespace OngekiFumenEditor.Kernel.Graphics.DrawCommands.DefaultDrawCommands
{
    /// <summary>
    /// Pops the current model matrix scope while presenting a command list.
    /// </summary>
    public sealed class PopModelMatrixCommand : DrawCommand
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PopModelMatrixCommand"/> class.
        /// </summary>
        public PopModelMatrixCommand()
        {
        }

        internal PopModelMatrixCommand Initialize()
        {
            return this;
        }

        /// <inheritdoc />
        protected override void ReturnToPoolCore()
        {
            ObjectPool<PopModelMatrixCommand>.Return(this);
        }
    }
}
