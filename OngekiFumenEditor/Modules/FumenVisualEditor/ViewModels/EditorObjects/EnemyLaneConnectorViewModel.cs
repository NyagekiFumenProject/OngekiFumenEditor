using OngekiFumenEditor.Base.EditorObjects;
using OngekiFumenEditor.Base.OngekiObjects.Beam;
using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Base.OngekiObjects.Wall;
using OngekiFumenEditor.Utils.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels.EditorObjects
{
    public class EnemyLaneConnectorViewModel : ConnectorViewModel<ConnectableObjectBase>
    {
        public override Brush LineBrush => Brushes.Yellow;

        public static DoubleCollection StaticEnemyLaneLineDashArray { get; } = new() { 10, 5 };
        public override DoubleCollection DefaultLineDashArray { get; } = StaticEnemyLaneLineDashArray;
    }
}
