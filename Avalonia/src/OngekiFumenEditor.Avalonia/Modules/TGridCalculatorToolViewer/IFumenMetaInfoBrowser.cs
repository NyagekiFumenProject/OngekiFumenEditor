using Gekimini.Avalonia.Framework;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.ViewModels;

namespace OngekiFumenEditor.Avalonia.Modules.TGridCalculatorToolViewer
{
	public interface ITGridCalculatorToolViewer : IToolViewModel
	{
		public FumenVisualEditorViewModel Editor { get; set; }
	}
}


