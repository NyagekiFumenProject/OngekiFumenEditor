using OngekiFumenEditor.Base.EditorObjects;
using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Base.OngekiObjects.Lane.Base;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels.OngekiObjects;
using OngekiFumenEditor.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Base.OngekiObjects
{
    public class Hold : ConnectableStartObject, ILaneDockable
    {
        private bool isCritical = false;
        public bool IsCritical
        {
            get { return isCritical; }
            set
            {
                isCritical = value;
                NotifyOfPropertyChange(() => IDShortName);
                NotifyOfPropertyChange(() => IsCritical);
                Children.ForEach(x => x.NotifyOfPropertyChange(() => IsCritical));
            }
        }

        private LaneStartBase referenceLaneStart = default;
        public LaneStartBase ReferenceLaneStart
        {
            get { return referenceLaneStart; }
            set
            {
                referenceLaneStart = value;
                NotifyOfPropertyChange(() => ReferenceLaneStart);
                Children?.FirstOrDefault()?.NotifyOfPropertyChange(() => ReferenceLaneStart);
            }
        }

        public override Type ModelViewType => typeof(HoldViewModel);

        public override string IDShortName => IsCritical ? "CHD" : "HLD";

        protected override ConnectorLineBase<ConnectableObjectBase> GenerateConnector(ConnectableObjectBase from, ConnectableObjectBase to)
        {
            return new HoldConnector()
            {
                From = from,
                To = to
            };
        }

        public override string Serialize(OngekiFumen fumenData)
        {
            var end = Children.FirstOrDefault();
            return $"{IDShortName} {ReferenceLaneStart.RecordId} {TGrid.Serialize(fumenData)} {XGrid.Unit} {XGrid.Grid} {end?.TGrid.Serialize(fumenData)} {end?.XGrid.Unit} {end?.XGrid.Grid}";
        }
    }
}
