using OngekiFumenEditor.Base.EditorObjects.Svg;
using OngekiFumenEditor.Modules.FumenVisualEditor.Graphics;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Kernel.Graphics.Drawing.DefaultDrawingImpl.SvgDrawing
{
    [Export(typeof(ISvgDrawing))]
    internal class DefaultSvgDrawing : CommonDrawingBase, ISvgDrawing
    {
        public void Draw(IDrawingContext target, SvgPrefabBase svg, Vector2 position)
        {

        }
    }
}
