using Caliburn.Micro;
using OngekiFumenEditor.Base.EditorObjects;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels.OngekiObjects;
using OngekiFumenEditor.Utils.ObjectPool;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;

namespace OngekiFumenEditor.Base.OngekiObjects.ConnectableObject
{
    public abstract class ConnectableStartObject : ConnectableObjectBase
    {
        public event Action<object, PropertyChangedEventArgs> ConnectableObjectsPropertyChanged;

        private List<ConnectableChildObjectBase> children = new();
        private List<ConnectorLineBase<ConnectableObjectBase>> connectors = new();
        public IEnumerable<ConnectableChildObjectBase> Children => children;

        private int recordId = -1;
        public override int RecordId { get => recordId; set => Set(ref recordId, value); }

        protected abstract ConnectorLineBase<ConnectableObjectBase> GenerateWallConnector(ConnectableObjectBase from, ConnectableObjectBase to);

        public ConnectableStartObject()
        {
            PropertyChanged += OnPropertyChanged;
        }

        public void AddChildWallObject(ConnectableChildObjectBase child)
        {
            if (!children.Contains(child))
            {
                child.PrevObject = children.LastOrDefault() ?? this as ConnectableObjectBase;
                children.Add(child);
                NotifyOfPropertyChange(() => Children);
                connectors.Add(GenerateWallConnector(child.PrevObject, child));
                child.PropertyChanged += OnPropertyChanged;
            }
            child.ReferenceStartObject = this;
        }

        public void RemoveChildWallObject(ConnectableChildObjectBase child)
        {
            children.Remove(child);

            connectors.RemoveAll(x => x.From == child || x.To == child);

            var prev = child.PrevObject;
            var next = children.FirstOrDefault(x => x.PrevObject == child);
            if (next is not null)
            {
                next.PrevObject = prev;
                connectors.Add(GenerateWallConnector(next.PrevObject, next));
            }
            child.PrevObject = default;

            child.ReferenceStartObject = default;
            child.PropertyChanged += OnPropertyChanged;
            NotifyOfPropertyChange(() => Children);
        }

        private void OnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            ConnectableObjectsPropertyChanged?.Invoke(sender, e);
        }

        public override IEnumerable<IDisplayableObject> GetDisplayableObjects()
        {
            foreach (var child in connectors)
                yield return child;
            yield return this;
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
}
