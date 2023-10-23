using Caliburn.Micro;
using OngekiFumenEditor.Base.Attributes;
using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels.OngekiObjects;
using OngekiFumenEditor.Utils;
using System;
using System.Collections.Generic;
using System.Linq;

namespace OngekiFumenEditor.Base.OngekiObjects.Beam
{
    public class BeamNext : ConnectableNextObject, IBeamObject
    {
        public override string IDShortName => "BMN";

        private int widthId = 2;
        public int WidthId
        {
            get => widthId;
            set => Set(ref widthId, value);
        }

        private XGrid obliqueSourceXGrid = null;
        [ObjectPropertyBrowserAllowSetNull]
        public XGrid ObliqueSourceXGrid
        {
            get { return obliqueSourceXGrid ?? ((IBeamObject)ReferenceStartObject).ObliqueSourceXGrid; }
            set
            {
                this.RegisterOrUnregisterPropertyChangeEvent(obliqueSourceXGrid, value);
                obliqueSourceXGrid = value;
                NotifyOfPropertyChange(() => ObliqueSourceXGrid);
            }
        }
    }
}
