using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;

namespace OngekiFumenEditor.Modules.EditorScriptExecutor.Kernel
{
	public class ScriptExecutionContext
	{
		public IEditorScriptExecutor ScriptExecutor { get; set; }
		public FumenVisualEditorViewModel Editor { get; set; }
	}
}
