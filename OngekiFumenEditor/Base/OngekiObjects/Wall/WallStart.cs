using Caliburn.Micro;
using OngekiFumenEditor.Base.EditorObjects;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels.OngekiObjects;
using OngekiFumenEditor.Utils.ObjectPool;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace OngekiFumenEditor.Base.OngekiObjects.Wall
{
    public abstract class WallStart : WallBase
    {
        private List<WallChildBase> children = new();
        private List<ConnectorLineBase<WallBase>> connectors = new();
        public IEnumerable<WallChildBase> Children => children;

        private int recordId = -1;
        public override int RecordId { get => recordId; set => Set(ref recordId, value); }

        protected abstract ConnectorLineBase<WallBase> GenerateWallConnector(WallBase from, WallBase to);

        public void AddChildWallObject(WallChildBase child)
        {
            if (!children.Contains(child))
            {
                child.PrevWall = children.LastOrDefault() ?? this as WallBase;
                children.Add(child);
                NotifyOfPropertyChange(() => Children);
                connectors.Add(GenerateWallConnector(child.PrevWall, child));
            }
            child.ReferenceWall = this;
        }

        public void RemoveChildWallObject(WallChildBase child)
        {
            children.Remove(child);

            connectors.RemoveAll(x => x.From == child || x.To == child);

            var prev = child.PrevWall;
            var next = children.FirstOrDefault(x => x.PrevWall == child);
            if (next is not null)
            {
                next.PrevWall = prev;
                connectors.Add(GenerateWallConnector(next.PrevWall, next));
            }
            child.PrevWall = default;

            child.ReferenceWall = default;

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
