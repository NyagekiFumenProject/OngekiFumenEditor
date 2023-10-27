namespace OngekiFumenEditor.Modules.FumenCheckerListViewer.Base.DefaultRulesImpl
{
	public struct CommonCheckResult : ICheckResult
	{
		public string RuleName { get; set; }

		public RuleSeverity Severity { get; set; }

		public string LocationDescription { get; set; }
		public string Description { get; set; }

		public INavigateBehavior NavigateBehavior { get; set; }
	}
}
