using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.ViewModels;
using Avalonia;

namespace OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Base.DropActions
{
	public interface IEditorDropHandler
	{
		void Drop(FumenVisualEditorViewModel editor, Point dragEndPoint);
	}
}

