using OngekiFumenEditor.Base;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;

namespace OngekiFumenEditor.Modules.FumenCheckerListViewer.Base.DefaultNavigateBehaviorImpl
{
	public class NavigateToTGridBehavior : INavigateBehavior
	{
		private readonly TGrid tGrid;

		public NavigateToTGridBehavior(TGrid tGrid)
		{
			this.tGrid = tGrid;
		}

		public void Navigate(FumenVisualEditorViewModel editor)
		{
			editor.ScrollTo(tGrid);
		}
	}
}
