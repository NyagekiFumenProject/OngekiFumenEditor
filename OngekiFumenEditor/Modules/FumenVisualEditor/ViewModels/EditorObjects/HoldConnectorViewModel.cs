using OngekiFumenEditor.Base.EditorObjects;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Base.OngekiObjects.Beam;
using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Base.OngekiObjects.Lane.Base;
using OngekiFumenEditor.Base.OngekiObjects.Wall;
using OngekiFumenEditor.Utils;
using OngekiFumenEditor.Utils.ObjectPool;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels.EditorObjects
{
    public class HoldConnectorViewModel : ConnectorViewModel
    {
        public ObservableCollection<LineSegment> Lines { get; } = new();

        private HoldConnector connector = default;
        public HoldConnector Connector
        {
            get => connector;
            set
            {
                connector = value;
                NotifyOfPropertyChange(() => Connector);
            }
        }

        private FumenVisualEditorViewModel editorViewModel;
        public FumenVisualEditorViewModel EditorViewModel
        {
            get
            {
                return editorViewModel;
            }
            set
            {
                Set(ref editorViewModel, value);
            }
        }

        public override void OnObjectCreated(object createFrom, FumenVisualEditorViewModel editorViewModel)
        {
            if (createFrom is HoldConnector connector)
                Connector = connector;
            EditorViewModel = editorViewModel;
        }

        public void RebuildLines()
        {
            foreach (var item in Lines)
                ObjectPool<LineSegment>.Return(item);
            Lines.Clear();
            if (EditorViewModel is null || Connector?.From is not Hold hold || hold.ReferenceLaneStart is not LaneStartBase refLane || hold.Children.FirstOrDefault() is not HoldEnd holdEnd)
                return;

            using var disp = ObjectPool<Dictionary<ConnectableObjectBase, Point>>.GetWithUsingDisposable(out var tempPointMap, out _);
            using var disp2 = ObjectPool<List<ConnectableObjectBase>>.GetWithUsingDisposable(out var allLaneNodes, out _);
            using var disp3 = ObjectPool<List<ConnectableObjectBase>>.GetWithUsingDisposable(out var pickNodes, out _);
            allLaneNodes.Clear();
            pickNodes.Clear();

            allLaneNodes.AddRange(refLane.Children.AsEnumerable<ConnectableObjectBase>().Prepend(refLane));
            var holdStartTGrid = hold.TGrid;
            var holdEndTGrid = holdEnd.TGrid;
            //allLaneNodes.Sort((a, b) => a.TGrid.CompareTo(b.TGrid));

            pickNodes.AddRange(allLaneNodes.Where(x => holdStartTGrid <= x.TGrid && x.TGrid <= holdEndTGrid));
            foreach (var node in pickNodes)
            {
                //calc point
                var x = XGridCalculator.ConvertXGridToX(node.XGrid, EditorViewModel);
                var y = TGridCalculator.ConvertTGridToY(node.TGrid, EditorViewModel) ?? 0;
                var segment = ObjectPool<LineSegment>.Get();
                segment.IsStroked = true;
                segment.Point = new Point(x, y);
                Lines.Add(segment);
            }

            //计算底部超出的(如果有的话)
            if (pickNodes.FirstOrDefault() is ConnectableObjectBase afterNode && allLaneNodes.FindPreviousOrDefault(afterNode) is ConnectableObjectBase firstNode)
            {
                var x = MathUtils.CalculateXFromBetweenObjects(firstNode, afterNode, EditorViewModel, TGridCalculator.ConvertYToTGrid(0, editorViewModel));
                var segment = ObjectPool<LineSegment>.Get();
                segment.IsStroked = true;
                segment.Point = new Point(x, 0);
                Lines.Insert(0, segment);
            }

            //计算顶部超出的(如果有的话)
            if (pickNodes.LastOrDefault() is ConnectableObjectBase frontNode && allLaneNodes.FindNextOrDefault(frontNode) is ConnectableObjectBase lastNode)
            {
                var x = MathUtils.CalculateXFromBetweenObjects(frontNode, lastNode, EditorViewModel, TGridCalculator.ConvertYToTGrid(editorViewModel.CanvasHeight, editorViewModel));
                var segment = ObjectPool<LineSegment>.Get();
                segment.IsStroked = true;
                segment.Point = new Point(x, editorViewModel.CanvasHeight);
                Lines.Add(segment);
            }
        }
    }
}
