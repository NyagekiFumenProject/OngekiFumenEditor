using OngekiFumenEditor.Base.EditorObjects;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels.OngekiObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Base.OngekiObjects.Wall
{
    public class WallLeftStart : WallStart
    {
        public override string IDShortName => "WLS";
        public override Type ModelViewType => typeof(WallStartViewModel<WallLeftStart>);

        protected override ConnectorLineBase<WallBase> GenerateWallConnector(WallBase from, WallBase to) => new WallLeftConnector()
        {
            From = from,
            To = to
        };
    }
}
