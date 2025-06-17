using OngekiFumenEditor.Base.EditorObjects.Svg;
using OngekiFumenEditor.Kernel.Graphics.OpenGL;
using System.ComponentModel.Composition;
using System.Numerics;

namespace OngekiFumenEditor.Kernel.Graphics.Drawing.DefaultDrawingImpl.SvgDrawing
{
    internal class DefaultSvgDrawing : CommonOpenGLDrawingBase, ISvgDrawing
	{
        public DefaultSvgDrawing(DefaultOpenGLRenderManager manager) : base(manager)
        {

        }

        public void Draw(IDrawingContext target, SvgPrefabBase svg, Vector2 position)
		{

		}
	}
}
