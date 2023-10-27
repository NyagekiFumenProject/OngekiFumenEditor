namespace OngekiFumenEditor.Modules.EditorScriptExecutor.Kernel.DefaultImpl
{
	public class DefaultCompletionItem : ICompletionItem
	{
		public string Name { get; set; }

		public string Description { get; set; }

		public int Priority { get; set; }
	}
}
