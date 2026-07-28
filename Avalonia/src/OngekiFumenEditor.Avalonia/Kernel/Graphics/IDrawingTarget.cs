using OngekiFumenEditor.Avalonia.Base;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Graphics;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Graphics.Drawing;
using System.Collections.Generic;

namespace OngekiFumenEditor.Avalonia.Kernel.Graphics
{
    public interface IDrawingTarget
    {
        void Initialize(IRenderManagerImpl impl);
    }
}

