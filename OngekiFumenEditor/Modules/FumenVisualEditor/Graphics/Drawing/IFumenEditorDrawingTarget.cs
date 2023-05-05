using OngekiFumenEditor.Kernel.Graphics;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Graphics.Drawing
{
    public interface IFumenEditorDrawingTarget : IDrawingTarget
    {
        DrawingVisible DefaultVisible { get; }
        DrawingVisible Visible { get; set; }
    }
}
