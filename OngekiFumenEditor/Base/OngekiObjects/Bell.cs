using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels.OngekiObjects;
using OngekiFumenEditor.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Base.OngekiObjects
{
    public class Bell : IOngekiObject, ITimelineObject, IHorizonPositionObject
    {
        public TGrid TGrid { get; set; }
        public XGrid XGrid { get; set; }

        public string Group => "BELL";
        public string Name => "Bell";

        public string IDShortName => "BEL";

        public string Serialize(OngekiFumen fumenData)
        {
            return $"{IDShortName} {TGrid.Serialize(fumenData)} {XGrid.Serialize(fumenData)}";
        }
    }
}
