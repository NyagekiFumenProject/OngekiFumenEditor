using Caliburn.Micro;
using OngekiFumenEditor.Base.EditorObjects;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels.OngekiObjects;
using OngekiFumenEditor.Utils.ObjectPool;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace OngekiFumenEditor.Base.OngekiObjects.CommonConnectable
{
    public abstract class ConnectableStart<T> : ConnectableObjectBase where T : ConnectorLineBase<ConnectableObjectBase>, new()
    {
        private List<ConnectableChildBase> children = new();
        private List<T> connectors = new();
        public IEnumerable<ConnectableChildBase> Children => children;

        private int recordId = -1;
        public override int RecordId { get => recordId; set => Set(ref recordId, value); }

        public override Type ModelViewType => typeof(BeamStartViewModel);

        public void AddChildBeamObject(ConnectableChildBase child)
        {
            if (!children.Contains(child))
            {
                child.PrevObject = children.LastOrDefault() ?? this as ConnectableObjectBase;
                children.Add(child);
                NotifyOfPropertyChange(() => Children);
                connectors.Add(new T()
                {
                    From = child.PrevObject,
                    To = child
                });
            }
            child.ReferenceObject = this;
        }

        public void RemoveChildBeamObject(ConnectableChildBase child)
        {
            children.Remove(child);

            connectors.RemoveAll(x => x.From == child || x.To == child);

            var prev = child.PrevObject;
            var next = children.FirstOrDefault(x => x.PrevObject == child);
            if (next is not null)
            {
                next.PrevObject = prev;
                connectors.Add(new T()
                {
                    From = next.PrevObject,
                    To = next
                });
            }
            child.PrevObject = default;

            child.ReferenceObject = default;

            NotifyOfPropertyChange(() => Children);
        }

        public override IEnumerable<IDisplayableObject> GetDisplayableObjects()
        {
            yield return this;
            foreach (var child in connectors)
                yield return child;
            foreach (var child in Children)
                yield return child;
        }

        public override string Serialize(OngekiFumen fumenData)
        {
            using var disp = ObjectPool<StringBuilder>.GetWithUsingDisposable(out var sb, out var isNew);
            if (!isNew)
                sb.Clear();

            sb.AppendLine(base.Serialize(fumenData));
            foreach (var child in Children)
                sb.AppendLine(child.Serialize(fumenData));

            return sb.ToString();
        }
    }

    public abstract class ConnectableStart : ConnectableStart<DefaultConnectorLine>
    {

    }
}
