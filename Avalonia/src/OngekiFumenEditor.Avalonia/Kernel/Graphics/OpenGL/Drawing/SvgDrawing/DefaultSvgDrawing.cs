using OngekiFumenEditor.Avalonia.Base.EditorObjects.Svg;
using OngekiFumenEditor.Avalonia.Kernel.Graphics.OpenGL;
using Injectio.Attributes;
using System.Numerics;

namespace OngekiFumenEditor.Avalonia.Kernel.Graphics.OpenGL.Drawing.SvgDrawing
{
    internal class DefaultSvgDrawing : CommonOpenGLDrawingBase, ISvgDrawing
	{
        public DefaultSvgDrawing(DefaultOpenGLRenderManagerImpl manager) : base(manager)
        {

        }

        public void Draw(IDrawingContext target, SvgPrefabBase svg, Vector2 position)
		{

		}
	}
}


