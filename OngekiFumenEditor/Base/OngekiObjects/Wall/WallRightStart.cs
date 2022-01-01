using OngekiFumenEditor.Base.EditorObjects;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels.OngekiObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Base.OngekiObjects.Wall
{
    public class WallRightStart : WallStart
    {
        public override string IDShortName => "WRS";
        public override Type ModelViewType => typeof(WallStartViewModel<WallRightStart>); 
        
        protected override ConnectorLineBase<WallBase> GenerateWallConnector(WallBase from, WallBase to) => new WallRightConnector()
        {
            From = from,
            To = to
        };
    }
}
