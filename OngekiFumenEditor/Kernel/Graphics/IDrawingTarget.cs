using OngekiFumenEditor.Base;
using OngekiFumenEditor.Modules.FumenVisualEditor.Graphics;
using OngekiFumenEditor.Modules.FumenVisualEditor.Graphics.Drawing;
using System.Collections.Generic;

namespace OngekiFumenEditor.Kernel.Graphics
{
    public interface IDrawingTarget
    {

        void Initialize(IRenderManagerImpl impl);
    }
}