using Caliburn.Micro;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels.OngekiObjects;
using System;
using System.Collections.Generic;
using System.Linq;

namespace OngekiFumenEditor.Base.OngekiObjects.Beam
{
    public class BeamStart : BeamBase
    {
        private List<BeamChildBase> children = new();
        private IEnumerable<BeamChildBase> Children => children;

        public override Type ModelViewType => typeof(BeamStartViewModel);

        public override string IDShortName => "BMS";

        public int RecordId { get; set; }

        public void AddChildBeamObject(BeamChildBase child)
        {
            if (!children.Contains(child))
                children.Add(child);
            child.ReferenceBeam = this;
        }

        public void RemoveChildBeamObject(BeamChildBase child)
        {
            children.Remove(child);
            child.ReferenceBeam = default;
        }
    }
}
