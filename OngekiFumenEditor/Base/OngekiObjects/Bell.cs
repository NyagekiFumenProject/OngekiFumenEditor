using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels.OngekiObjects;
using OngekiFumenEditor.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Base.OngekiObjects
{
    public class Bell : IOngekiObject, ITimelineObject, IHorizonPositionObject , IDisplayableObject
    {
        public TGrid TGrid { get; set; } = new TGrid();
        public XGrid XGrid { get; set; } = new XGrid();

        public string Group => "BELL";
        public string Name => "Bell";

        public string IDShortName => "BEL";

        public Type ModelViewType => typeof(BellViewModel);

        public int CompareTo(object obj)
        {
            return TGrid.CompareTo((obj as ITimelineObject)?.TGrid);
        }

        public string Serialize(OngekiFumen fumenData)
        {
            return $"{IDShortName} {TGrid.Serialize(fumenData)} {XGrid.Serialize(fumenData)}";
        }
    }
}
