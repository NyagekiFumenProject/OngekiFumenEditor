using Caliburn.Micro;
using OngekiFumenEditor.Base.EditorObjects;
using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels.OngekiObjects;
using OngekiFumenEditor.Utils.ObjectPool;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace OngekiFumenEditor.Base.OngekiObjects.Beam
{
    public class BeamStart : ConnectableStartObject, IBeamObject
    {
        public override string IDShortName => "BMS";
        private int widthId = 2;
        public int WidthId
        {
            get => widthId;
            set => Set(ref widthId, value);
        }
        public override Type NextType => typeof(BeamNext);
        public override Type EndType => typeof(BeamEnd);

        protected override ConnectorLineBase<ConnectableObjectBase> GenerateConnector(ConnectableObjectBase from, ConnectableObjectBase to)
        {
            return GenerateConnectorInternal<BeamConnector>(from, to);
        }
    }
}
