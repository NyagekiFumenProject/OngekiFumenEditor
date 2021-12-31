using Caliburn.Micro;
using OngekiFumenEditor.Base.EditorObjects;
using OngekiFumenEditor.Base.OngekiObjects.Beam;
using OngekiFumenEditor.Base.OngekiObjects.Wall;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels.EditorObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Base.EditorObjects
{
    public class WallConnector : ConnectorLineBase<WallBase>
    {
        public override Type ModelViewType => typeof(WallConnectorViewModel);
    }
}
