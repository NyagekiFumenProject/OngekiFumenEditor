using OngekiFumenEditor.Base;
using OngekiFumenEditor.Modules.FumenVisualEditor.Graphics;
using System.Collections.Generic;

namespace OngekiFumenEditor.Kernel.Graphics
{
	public interface IDrawingTarget
	{
		IEnumerable<string> DrawTargetID { get; }
		int DefaultRenderOrder { get; }
		int CurrentRenderOrder { get; set; }

		void Begin(IFumenEditorDrawingContext target);
		void Post(OngekiObjectBase ongekiObject);
		void End();
	}
}