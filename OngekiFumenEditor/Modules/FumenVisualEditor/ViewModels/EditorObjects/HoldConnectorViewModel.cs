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
using OpenTK.Mathematics;
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
    [MapToView(ViewType = typeof(HoldConnectorView))]
    public class HoldConnectorViewModel : ConnectorViewModel
    {
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

            void addPoint(Vector2 gv2)
            {
                var y = (float)(EditorViewModel.TotalDurationHeight - TGridCalculator.ConvertTGridToY(new(gv2.Y / hold.TGrid.ResT, 0), EditorViewModel));
                var x = (float)XGridCalculator.ConvertXGridToX(new(gv2.X / hold.XGrid.ResX, 0), EditorViewModel);

                var seg = new LineSegment(new(x + 0.0000001 * MathUtils.Random(100), y), true);
                seg.Freeze();
                Lines.Add(seg);
            }

            using var d = ObjectPool<List<Vector2>>.GetWithUsingDisposable(out var points, out _);
            points.Clear();
            var isVaild = true;
            var beginTotalGrid = hold.TGrid.TotalGrid;
            var endTotalGrid = hold.HoldEnd.TGrid.TotalGrid;

            foreach ((var p, var invaild) in refLane.GenAllPath())
            {
                /*
                if (!invaild)
                {
                    isVaild = false;
                    return;
                }
                */
                if (!(p.Y >= beginTotalGrid && p.Y <= endTotalGrid))
                    continue;

                points.Add(p);
            }

            if (isVaild)
            {
                addPoint(new(hold.XGrid.TotalGrid, hold.TGrid.TotalGrid));
                foreach (var gridVec2 in points)
                    addPoint(gridVec2);
                addPoint(new(hold.HoldEnd.XGrid.TotalGrid, hold.HoldEnd.TGrid.TotalGrid));
            }

            NotifyOfPropertyChange(() => Lines);
        }

        public override void OnEditorRedrawObjects()
        {
            RebuildLines();
            base.OnEditorRedrawObjects();
        }
    }
}
