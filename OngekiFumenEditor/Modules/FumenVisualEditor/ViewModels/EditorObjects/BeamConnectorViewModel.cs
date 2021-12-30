using OngekiFumenEditor.Base.OngekiObjects.Beam;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels.EditorObjects
{
    public class BeamConnectorViewModel : ConnectorViewModel<BeamBase>
    {
        public override Brush LineBrush => Brushes.DeepPink;
    }
}
