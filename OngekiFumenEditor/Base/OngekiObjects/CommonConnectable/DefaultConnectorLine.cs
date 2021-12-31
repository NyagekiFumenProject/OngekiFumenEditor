using OngekiFumenEditor.Base.EditorObjects;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels.EditorObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Base.OngekiObjects.CommonConnectable
{
    public class DefaultConnectorLine : ConnectorLineBase<ConnectableObjectBase>
    {
        public override Type ModelViewType => typeof(DefaultConnectorViewModel);
    }
}
