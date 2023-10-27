namespace OngekiFumenEditor.Modules.EditorScriptExecutor.Kernel
{
	public interface ICompletionItem
	{
		string Name { get; }
		string Description { get; }
		int Priority { get; }
	}
}
