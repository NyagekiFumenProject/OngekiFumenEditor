using OngekiFumenEditor.Avalonia.Base;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.ViewModels;

namespace OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Base
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


