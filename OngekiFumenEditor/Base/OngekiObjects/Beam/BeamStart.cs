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
        private List<BeamConnector> connectors = new();
        public IEnumerable<BeamChildBase> Children => children;

        private int recordId = -1;
        public override int RecordId { get => recordId; set => Set(ref recordId, value); }

        public override Type ModelViewType => typeof(BeamStartViewModel);

        public override string IDShortName => "BMS";

        public void AddChildBeamObject(BeamChildBase child)
        {
            if (!children.Contains(child))
            {
                child.PrevBeam = children.LastOrDefault() ?? this as BeamBase;
                children.Add(child);
                NotifyOfPropertyChange(() => Children);
                connectors.Add(new BeamConnector()
                {
                    From = child.PrevBeam,
                    To = child
                });
            }
            child.ReferenceBeam = this;
        }

        public void RemoveChildBeamObject(BeamChildBase child)
        {
            children.Remove(child);

            connectors.RemoveAll(x => x.From == child || x.To == child);

            var prev = child.PrevBeam;
            var next = children.FirstOrDefault(x => x.PrevBeam == child);
            if (next is not null)
                next.PrevBeam = prev;
            child.PrevBeam = default;

            child.ReferenceBeam = default;

            NotifyOfPropertyChange(() => Children);
        }

        public override IEnumerable<IDisplayableObject> GetDisplayableObjects()
        {
            yield return this;
            foreach (var child in Children)
                yield return child;
            foreach (var child in connectors)
                yield return child;
        }
    }
}
