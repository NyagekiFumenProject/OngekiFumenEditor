namespace OngekiFumenEditor.Modules.FumenCheckerListViewer.Base
{
	public interface ICheckResult
	{
		string RuleName { get; }
		RuleSeverity Severity { get; }
		string LocationDescription { get; }

		string Description { get; }

		INavigateBehavior NavigateBehavior { get; }
	}
}
