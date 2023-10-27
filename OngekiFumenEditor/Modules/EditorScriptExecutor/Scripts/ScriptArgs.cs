using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using System.Reflection;

namespace OngekiFumenEditor.Modules.EditorScriptExecutor.Scripts
{
	public static class ScriptArgs
	{
		public static FumenVisualEditorViewModel TargetEditor => ScriptArgsGlobalStore.GetCurrentEditor(Assembly.GetCallingAssembly());
	}
}
