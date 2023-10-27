using OngekiFumenEditor.Base.EditorObjects.Svg;
using System.ComponentModel.Composition;
using System.Numerics;

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
