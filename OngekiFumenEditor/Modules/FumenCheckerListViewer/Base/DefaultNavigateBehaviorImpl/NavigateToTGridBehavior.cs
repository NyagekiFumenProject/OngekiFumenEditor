using OngekiFumenEditor.Base;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
