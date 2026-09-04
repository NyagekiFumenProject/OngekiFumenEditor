using OngekiFumenEditor.Avalonia.Utils.ObjectPool;

namespace OngekiFumenEditor.Avalonia.Kernel.Graphics.DrawCommands.DefaultDrawCommands
{
    /// <summary>
    /// Pops the current model matrix scope while presenting a command list.
    /// </summary>
    public sealed class PopModelMatrixCommand : DrawCommand
    {
        public PopModelMatrixCommand()
        {
        }

        internal PopModelMatrixCommand Initialize()
        {
            return this;
        }

        protected override void ReturnToPoolCore()
        {
            ObjectPool<PopModelMatrixCommand>.Return(this);
        }
    }
}