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
        public IEnumerable<BeamChildBase> Children => children;

        public override Type ModelViewType => typeof(BeamStartViewModel);

        public override string IDShortName => "BMS";

        public void AddChildBeamObject(BeamChildBase child)
        {
            if (!children.Contains(child))
            {
                child.PrevBeam = children.LastOrDefault() ?? this as BeamBase;
                children.Add(child);
                NotifyOfPropertyChange(() => Children);
            }
            child.ReferenceBeam = this;
        }

        public void RemoveChildBeamObject(BeamChildBase child)
        {
            children.Remove(child);
            child.PrevBeam = default;
            child.ReferenceBeam = default;
            NotifyOfPropertyChange(() => Children);
        }
    }
}
