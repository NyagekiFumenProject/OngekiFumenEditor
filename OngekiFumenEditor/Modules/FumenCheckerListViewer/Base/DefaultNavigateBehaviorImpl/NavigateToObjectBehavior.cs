using OngekiFumenEditor.Base;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;

namespace OngekiFumenEditor.Modules.FumenCheckerListViewer.Base.DefaultNavigateBehaviorImpl
{
	public class NavigateToObjectBehavior : INavigateBehavior
	{
		private readonly OngekiTimelineObjectBase ongekiObject;

		public NavigateToObjectBehavior(OngekiTimelineObjectBase ongekiObject)
		{
			this.ongekiObject = ongekiObject;
		}

		public void Navigate(FumenVisualEditorViewModel editor)
		{
			editor.ScrollTo(ongekiObject);
			editor.NotifyObjectClicked(ongekiObject);
		}
	}
}
