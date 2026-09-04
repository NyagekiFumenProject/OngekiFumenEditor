using OngekiFumenEditor.Avalonia.Utils.ObjectPool;

namespace OngekiFumenEditor.Avalonia.Kernel.Graphics.DrawCommands.DefaultDrawCommands
{
    /// <summary>
    /// Pops the current projection matrix scope while presenting a command list.
    /// </summary>
    public sealed class PopProjectionMatrixCommand : DrawCommand
    {
        public PopProjectionMatrixCommand()
        {
        }

        internal PopProjectionMatrixCommand Initialize()
        {
            return this;
        }

        protected override void ReturnToPoolCore()
        {
            ObjectPool<PopProjectionMatrixCommand>.Return(this);
        }
    }
}