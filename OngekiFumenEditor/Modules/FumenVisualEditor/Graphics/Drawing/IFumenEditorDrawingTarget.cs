using OngekiFumenEditor.Kernel.Graphics;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Graphics.Drawing
{
	public interface IFumenEditorDrawingTarget : IDrawingTarget
	{
		DrawingVisible DefaultVisible { get; }
		DrawingVisible Visible { get; set; }
	}
}
