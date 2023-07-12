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
        public const int LEAD_DURATION = 250;

        public override string IDShortName => "BMS";
        private int widthId = 2;
        public int WidthId
        {
            get => widthId;
            set => Set(ref widthId, value);
        }

        public override ConnectableNextObject CreateNextObject() => new BeamNext();
        public override ConnectableEndObject CreateEndObject() => new BeamEnd();
    }
}
