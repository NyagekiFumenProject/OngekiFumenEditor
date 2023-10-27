using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using System.Windows;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Base.DropActions
{
	public interface IEditorDropHandler
	{
		void Drop(FumenVisualEditorViewModel editor, Point dragEndPoint);
	}
}
