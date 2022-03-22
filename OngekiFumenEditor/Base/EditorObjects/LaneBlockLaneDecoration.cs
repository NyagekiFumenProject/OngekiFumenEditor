using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels.EditorObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Base.EditorObjects
{
    public class LaneBlockLaneDecoration : ConnectorLineBase<OngekiTimelineObjectBase>
    {
        public override Type ModelViewType => typeof(LaneBlockLaneDecorationViewModel);
    }
}
