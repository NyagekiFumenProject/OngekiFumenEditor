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
using System.Windows.Media;

namespace OngekiFumenEditor.Base.EditorObjects
{
    public abstract class WallConnector : ConnectorLineBase<WallBase>
    {
        public abstract Color LineColor { get; }
        public override Type ModelViewType => typeof(WallConnectorViewModel);
    }

    public class WallLeftConnector : WallConnector
    {
        static readonly Color DefaultColor = Colors.Aqua;
        public override Color LineColor => DefaultColor;
    }

    public class WallRightConnector : WallConnector
    {
        static readonly Color DefaultColor = Colors.OrangeRed;
        public override Color LineColor => DefaultColor;
    }
}
