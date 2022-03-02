using OngekiFumenEditor.Base.EditorObjects;
using OngekiFumenEditor.Base.OngekiObjects.Beam;
using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Base.OngekiObjects.Wall;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels.EditorObjects
{
    public class LaneConnectorViewModel : ConnectorViewModel<ConnectableObjectBase>
    {
        private SolidColorBrush lineBrush;
        public override Brush LineBrush => lineBrush ??= new SolidColorBrush((Connector as LaneConnector).LineColor);

        public override void NotifyOfPropertyChange([CallerMemberName] string propertyName = null)
        {
            if (propertyName == nameof(ConnectorViewModel<ConnectableObjectBase>.LineBrush))
                lineBrush.Color = (Connector as LaneConnector).LineColor;
            base.NotifyOfPropertyChange(propertyName);
        }
    }
}
