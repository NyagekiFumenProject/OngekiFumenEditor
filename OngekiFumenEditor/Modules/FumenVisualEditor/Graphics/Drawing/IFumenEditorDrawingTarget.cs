using OngekiFumenEditor.Base;
using OngekiFumenEditor.Kernel.Graphics;
using System.Collections.Generic;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Graphics.Drawing
{
	public interface IFumenEditorDrawingTarget : IDrawingTarget
    {
        IEnumerable<string> DrawTargetID { get; }
        DrawingVisible DefaultVisible { get; }
		DrawingVisible Visible { get; set; }
        int DefaultRenderOrder { get; }
        int CurrentRenderOrder { get; set; }

        void Begin(IFumenEditorDrawingContext target);
        void Post(OngekiObjectBase ongekiObject);
        void End();
    }
}
