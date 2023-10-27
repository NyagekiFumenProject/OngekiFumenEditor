using Gemini.Framework;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;

namespace OngekiFumenEditor.Modules.TGridCalculatorToolViewer
{
	public interface ITGridCalculatorToolViewer : ITool
	{
		public FumenVisualEditorViewModel Editor { get; set; }
	}
}
