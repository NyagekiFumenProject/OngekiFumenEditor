using Caliburn.Micro;
using OngekiFumenEditor.Kernel.Graphics;
using System.Numerics;
using static OngekiFumenEditor.Kernel.Graphics.ILineDrawing;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Graphics.Drawing.Editors
{
	public class DrawPlayableAreaHelper
	{
		private ILineDrawing lineDrawing;
		private Vector4 color = new(1, 0, 0, 1);

		LineVertex[] vertices = new LineVertex[2];

		public DrawPlayableAreaHelper()
		{
			lineDrawing = IoC.Get<ISimpleLineDrawing>();
		}

		public void Draw(IFumenEditorDrawingContext target)
		{
			if (!target.Editor.IsDesignMode)
				return;

			var y = (float)target.Editor.TotalDurationHeight;

			vertices[0] = new(new(0, y), color, VertexDash.Solider);
			vertices[1] = new(new(target.ViewWidth, y), color, VertexDash.Solider);

			lineDrawing.Draw(target, vertices, 4);
		}
	}
}
