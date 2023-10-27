using System;

namespace OngekiFumenEditor.Kernel.Graphics
{
	public interface IStaticVBODrawing : IDrawing
	{
		public interface IVBOHandle : IDisposable
		{

		}
		void DrawVBO(IDrawingContext target, IVBOHandle vbo);
	}
}
