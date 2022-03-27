using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels.EditorObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Base.EditorObjects.LaneCurve
{
    public class LaneCurveObjectConnector : ConnectorLineBase<ConnectableObjectBase>
    {
        public override Type ModelViewType => typeof(LaneCurveObjectConnectorViewModel);
    }
}
