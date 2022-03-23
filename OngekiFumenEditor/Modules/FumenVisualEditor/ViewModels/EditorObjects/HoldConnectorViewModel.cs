using Caliburn.Micro;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.EditorObjects;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Base.OngekiObjects.Beam;
using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Base.OngekiObjects.Lane.Base;
using OngekiFumenEditor.Base.OngekiObjects.Wall;
using OngekiFumenEditor.Modules.FumenVisualEditor.Views.EditorObjects;
using OngekiFumenEditor.Utils;
using OngekiFumenEditor.Utils.Attributes;
using OngekiFumenEditor.Utils.ObjectPool;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels.EditorObjects
{
    public abstract class BindableTGridSegement : PropertyChangedBase
    {
        public LineSegment Segment { get; } = new LineSegment();
        public abstract bool RecalcPoint(XGrid directValue = default);
    }

    public class BindableTGridSegement<T> : BindableTGridSegement where T : INotifyPropertyChanged, IHorizonPositionObject, ITimelineObject
    {
        private T bindObject;
        public T BindObject
        {
            get => bindObject;
            set
            {
                Set(ref bindObject, value);
            }
        }

        private FumenVisualEditorViewModel bindFumenEditor;
        public FumenVisualEditorViewModel BindFumenEditor
        {
            get => bindFumenEditor;
            set
            {
                Set(ref bindFumenEditor, value);
            }
        }

        public override bool RecalcPoint(XGrid directValue = default)
        {
            if (BindObject is null || BindFumenEditor is null)
                return false;

            var x = XGridCalculator.ConvertXGridToX(BindObject.XGrid, BindFumenEditor)
                //为啥要加个随机数呢，是因为PathSegmentCollection重新绘制会有缓存机制，如果一个点没有变动，那么就不会绘制此点以及后面的点(我猜的)
                + 0.0000001 * MathUtils.Random(100);
            var y = BindFumenEditor.TotalDurationHeight - TGridCalculator.ConvertTGridToY(BindObject.TGrid, BindFumenEditor);

            Segment.Point = new Point(x, y);
            return true;
        }
    }

    [MapToView(ViewType = typeof(HoldConnectorView))]
    public class HoldConnectorViewModel : ConnectorViewModel
    {
        private Dictionary<object, BindableTGridSegement> map = new();
        public PathSegmentCollection Lines { get; } = new();

        private HoldConnector connector = default;
        public HoldConnector Connector
        {
            get => connector;
            set
            {
                /*
                 实时监听以下的物件位置变动:
                    Hold开头 ，HoldEnd ，还有对应的Lane线
                 有变动了就更新一下线
                 */
                if ((Connector?.From as Hold)?.ReferenceLaneStart is ConnectableStartObject old)
                    old.ConnectableObjectsPropertyChanged -= ConnectableObjectsPropertyChanged;
                if (Connector?.From is Hold oldHold)
                    oldHold.PropertyChanged -= ConnectableObjectsPropertyChanged;
                if (Connector?.To is HoldEnd oldEnd)
                    oldEnd.PropertyChanged -= ConnectableObjectsPropertyChanged;

                if ((value?.From as Hold)?.ReferenceLaneStart is ConnectableStartObject @new)
                    @new.ConnectableObjectsPropertyChanged += ConnectableObjectsPropertyChanged;
                if (value?.From is Hold newHold)
                    newHold.PropertyChanged += ConnectableObjectsPropertyChanged;
                if (value?.To is HoldEnd newEnd)
                    newEnd.PropertyChanged += ConnectableObjectsPropertyChanged;

                Set(ref connector, value);
            }
        }

        public Brush LineBrush => (Connector.From as Hold)?.ReferenceLaneStart?.LaneType switch
        {
            LaneType.Left => Brushes.OrangeRed,
            LaneType.Center => Brushes.Green,
            LaneType.Right => Brushes.Blue,
            LaneType.WallLeft or LaneType.WallRight => Brushes.Pink,
            _ => default
        };

        public override IDisplayableObject DisplayableObject => Connector;

        private void ConnectableObjectsPropertyChanged(object arg1, PropertyChangedEventArgs arg2)
        {
            switch (arg2.PropertyName)
            {
                case nameof(TGrid):
                case nameof(XGrid):
                    RebuildLines();
                    break;
                case nameof(Hold.ReferenceLaneStart):
                    NotifyOfPropertyChange(() => LineBrush);
                    break;
                default:
                    break;
            }
        }

        public override void OnObjectCreated(object createFrom, FumenVisualEditorViewModel editorViewModel)
        {
            if (createFrom is HoldConnector connector)
                Connector = connector;
            EditorViewModel = editorViewModel;
            RebuildLines();
        }

        public void RebuildLines()
        {
            if (EditorViewModel is null ||
                Connector?.From is not Hold hold ||
                hold.ReferenceLaneStart is not LaneStartBase refLane ||
                hold.Children.FirstOrDefault() is not HoldEnd holdEnd)
                return;
            Lines.Clear();

            void Upsert<T>(T obj) where T : INotifyPropertyChanged, IHorizonPositionObject, ITimelineObject
            {
                if (map.TryGetValue(obj, out var seg))
                {
                    if (seg.RecalcPoint())
                        Lines.Add(seg.Segment);
                    return;
                }
                var bind = new BindableTGridSegement<T>()
                {
                    BindFumenEditor = EditorViewModel,
                    BindObject = obj,
                };
                map[bind.BindObject] = bind;
                if (bind.RecalcPoint())
                    Lines.Add(bind.Segment);
            }

            Upsert(hold);
            foreach (var node in refLane.Children.AsEnumerable<ConnectableObjectBase>().Where(x => hold.TGrid <= x.TGrid && x.TGrid <= holdEnd.TGrid))
                Upsert(node);
            Upsert(holdEnd);

            NotifyOfPropertyChange(() => Lines);
        }

        public override void OnEditorRedrawObjects()
        {
            RebuildLines();
            base.OnEditorRedrawObjects();
        }
    }
}
