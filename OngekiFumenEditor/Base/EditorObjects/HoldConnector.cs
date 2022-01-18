using Caliburn.Micro;
using OngekiFumenEditor.Base.EditorObjects;
using OngekiFumenEditor.Base.OngekiObjects.Beam;
using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Base.OngekiObjects.Wall;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels.EditorObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace OngekiFumenEditor.Base.EditorObjects
{
    public class HoldConnector : ConnectorLineBase<ConnectableObjectBase>
    {
        public override Type ModelViewType => typeof(HoldConnectorViewModel);
    }
}
