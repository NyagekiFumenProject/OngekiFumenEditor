using OngekiFumenEditor.Avalonia.Base;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.ViewModels;

namespace OngekiFumenEditor.Avalonia.Modules.FumenCheckerListViewer.Base.DefaultNavigateBehaviorImpl
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


