using OngekiFumenEditor.Base.EditorObjects;
using OngekiFumenEditor.Base.OngekiObjects.Beam;
using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Base.OngekiObjects.Wall;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels.EditorObjects
{
    public class WallConnectorViewModel : ConnectorViewModel<ConnectableObjectBase>
    {
        private Brush lineBrush;
        public override Brush LineBrush => lineBrush ??= new SolidColorBrush((Connector as LaneConnector).LineColor);
    }
}
