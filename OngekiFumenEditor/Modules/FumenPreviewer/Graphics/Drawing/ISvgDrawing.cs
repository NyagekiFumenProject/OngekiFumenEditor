using OngekiFumenEditor.Base.EditorObjects.Svg;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using static OngekiFumenEditor.Modules.FumenPreviewer.Graphics.Drawing.ILineDrawing;

namespace OngekiFumenEditor.Modules.FumenPreviewer.Graphics.Drawing
{
    public interface ISvgDrawing : IDrawing
    {
        void Draw(IFumenEditorDrawingContext target, SvgPrefabBase svg, Vector2 position);
    }
}
