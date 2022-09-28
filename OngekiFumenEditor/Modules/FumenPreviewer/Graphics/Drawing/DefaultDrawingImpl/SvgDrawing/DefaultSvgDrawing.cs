using OngekiFumenEditor.Base.EditorObjects.Svg;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Modules.FumenPreviewer.Graphics.Drawing.DefaultDrawingImpl.SvgDrawing
{
    [Export(typeof(ISvgDrawing))]
    internal class DefaultSvgDrawing : CommonDrawingBase, ISvgDrawing
    {
        public void Draw(IFumenEditorDrawingContext target, SvgPrefabBase svg, Vector2 position)
        {

        }
    }
}
