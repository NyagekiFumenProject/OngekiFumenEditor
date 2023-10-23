using Caliburn.Micro;
using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels.OngekiObjects;
using OngekiFumenEditor.Utils;
using System;
using System.Collections.Generic;
using System.Linq;

namespace OngekiFumenEditor.Base.OngekiObjects.Beam
{
    public class BeamEnd : ConnectableEndObject, IBeamObject
    {
        public override string IDShortName => "BME";

        private int widthId = 2;
        public int WidthId
        {
            get => widthId;
            set => Set(ref widthId, value);
        }

        private XGrid obliqueSourceXGrid = null;
        public XGrid ObliqueSourceXGrid
        {
            get { return obliqueSourceXGrid; }
            set
            {
                this.RegisterOrUnregisterPropertyChangeEvent(obliqueSourceXGrid, value);
                obliqueSourceXGrid = value;
                NotifyOfPropertyChange(() => ObliqueSourceXGrid);
            }
        }
    }
}
