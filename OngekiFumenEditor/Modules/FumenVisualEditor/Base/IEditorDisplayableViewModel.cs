using OngekiFumenEditor.Base;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Base
{
	public interface IEditorDisplayableViewModel
	{
		int RenderOrderZ { get; }
		bool NeedCanvasPointsBinding { get; }
		IDisplayableObject DisplayableObject { get; }

		void OnObjectCreated(object createFrom, FumenVisualEditorViewModel editorViewModel);
		void OnEditorRedrawObjects();
	}
}
