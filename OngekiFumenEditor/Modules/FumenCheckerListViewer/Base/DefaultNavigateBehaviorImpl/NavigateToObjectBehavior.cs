using OngekiFumenEditor.Base;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
