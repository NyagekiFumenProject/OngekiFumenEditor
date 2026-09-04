using OngekiFumenEditor.Avalonia.Utils.ObjectPool;

namespace OngekiFumenEditor.Avalonia.Kernel.Graphics.DrawCommands.DefaultDrawCommands
{
    /// <summary>
    /// Pops the current view matrix scope while presenting a command list.
    /// </summary>
    public sealed class PopViewMatrixCommand : DrawCommand
    {
        public PopViewMatrixCommand()
        {
        }

        internal PopViewMatrixCommand Initialize()
        {
            return this;
        }

        protected override void ReturnToPoolCore()
        {
            ObjectPool<PopViewMatrixCommand>.Return(this);
        }
    }
}