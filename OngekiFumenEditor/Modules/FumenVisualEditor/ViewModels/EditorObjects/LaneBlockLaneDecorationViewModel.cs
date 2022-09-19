using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OngekiFumenEditor.Utils.Attributes;
using System.Threading.Tasks;
using System.Windows.Media;
using OngekiFumenEditor.Base.EditorObjects;
using OngekiFumenEditor.Base.OngekiObjects;
using static OngekiFumenEditor.Base.OngekiObjects.LaneBlockArea;
using System.ComponentModel;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Utils;
using OngekiFumenEditor.Modules.FumenVisualEditor.Views.EditorObjects;
using System.Windows;
using OngekiFumenEditor.Base.OngekiObjects.Lane.Base;
using OngekiFumenEditor.Utils.ObjectPool;
using OpenTK.Mathematics;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels.EditorObjects
{
    [MapToView(ViewType = typeof(LaneBlockLaneDecorationView))]
    public class LaneBlockLaneDecorationViewModel : ConnectorViewModel
    {
        public PathSegmentCollection Lines { get; } = new();
        public HashSet<INotifyPropertyChanged> listeners = new();
        private LaneBlockLaneDecoration decoration = default;
        public LaneBlockLaneDecoration Decoration
        {
            get => decoration;
            set
            {
                /*
                 实时监听以下的物件位置变动:
                    Hold开头 ，HoldEnd ，还有对应的Lane线
                 有变动了就更新一下线
                 */
                if (Decoration?.From is LaneBlockArea oldLbk)
                    oldLbk.PropertyChanged -= ConnectableObjectsPropertyChanged;
                if (Decoration?.To is LaneBlockAreaEndIndicator oldLbkEnd)
                    oldLbkEnd.PropertyChanged -= ConnectableObjectsPropertyChanged;

                if (value?.From is LaneBlockArea newLbk)
                    newLbk.PropertyChanged += ConnectableObjectsPropertyChanged;
                if (value?.To is LaneBlockAreaEndIndicator newLbkEnd)
                    newLbkEnd.PropertyChanged += ConnectableObjectsPropertyChanged;

                Set(ref decoration, value);
            }
        }

        private Brush lineBrush = Brushes.White;
        public Brush LineBrush
        {
            get => lineBrush;
            set => Set(ref lineBrush, value);
        }

        public override IDisplayableObject DisplayableObject => Decoration;

        private void ConnectableObjectsPropertyChanged(object arg1, PropertyChangedEventArgs arg2)
        {
            switch (arg2.PropertyName)
            {
                case nameof(LaneBlockArea.TGrid):
                case nameof(LaneBlockArea.Direction):
                    RebuildLines();
                    break;
                default:
                    break;
            }
        }

        public override void OnObjectCreated(object createFrom, FumenVisualEditorViewModel editorViewModel)
        {
            if (createFrom is LaneBlockLaneDecoration connector)
                Decoration = connector;
            EditorViewModel = editorViewModel;
            RebuildLines();
        }

        public void RebuildLines()
        {
            if (EditorViewModel is null ||
                Decoration?.From is not LaneBlockArea lbk ||
                lbk.EndIndicator is not LaneBlockAreaEndIndicator lbkEnd)
                return;
            Lines.Clear();

            foreach (var p in listeners)
                p.PropertyChanged -= O_PropertyChanged;
            listeners.Clear();

            void RegisterNotifyListener(INotifyPropertyChanged o)
            {
                o.PropertyChanged += O_PropertyChanged;
                listeners.Add(o);
            }

            void PostPointByXTGrid(XGrid xGrid, TGrid tGrid, bool isStroke = true)
            {
                if (xGrid is null)
                    return;
                var x = XGridCalculator.ConvertXGridToX(xGrid, EditorViewModel) + 0.0000001 * MathUtils.Random(100);
                var y = EditorViewModel.TotalDurationHeight - TGridCalculator.ConvertTGridToY(tGrid, EditorViewModel);
                var segement = new LineSegment(new Point(x, y), isStroke);
                segement.Freeze();
                Lines.Add(segement);
            }

            void PostPointByTGrid(ConnectableChildObjectBase obj, TGrid grid, bool isStroke = true)
            {
                var xGrid = obj.CalulateXGrid(grid);
                PostPointByXTGrid(xGrid, grid, isStroke);
            }

            void ProcessConnectable(ConnectableChildObjectBase obj, TGrid minTGrid, TGrid maxTGrid)
            {
                var minTotalGrid = minTGrid.TotalGrid;
                var maxTotalGrid = maxTGrid.TotalGrid;

                RegisterNotifyListener(obj);

                if (!obj.IsCurvePath)
                {
                    //直线，优化
                    PostPointByTGrid(obj, minTGrid);
                    PostPointByTGrid(obj, maxTGrid);
                }
                else
                {
                    //PostPointByXTGrid(obj.CalulateXGrid(minTGrid), minTGrid);
                    using var d = ObjectPool<List<Vector2>>.GetWithUsingDisposable(out var list, out _);
                    list.Clear();

                    foreach ((var gridVec2, var isVaild) in obj.GetConnectionPaths().Where(x => x.pos.Y <= maxTotalGrid && x.pos.Y >= minTotalGrid))
                    {
                        if (!isVaild)
                        {
                            PostPointByXTGrid(obj.PrevObject.XGrid, minTGrid, false);
                            PostPointByXTGrid(obj.XGrid, maxTGrid, false);
                            return;
                        }
                        list.Add(gridVec2);
                    }
                    foreach (var gridVec2 in list)
                        PostPointByXTGrid(new(gridVec2.X / obj.XGrid.ResX), new(gridVec2.Y / obj.TGrid.ResT));
                }
            }

            void ProcessWallLane(LaneStartBase wallStartLane, TGrid minTGrid, TGrid maxTGrid)
            {
                RegisterNotifyListener(wallStartLane);

                foreach (var child in wallStartLane.Children)
                {
                    if (child.TGrid < minTGrid)
                        continue;
                    if (child.PrevObject.TGrid > maxTGrid)
                        break;

                    var childMinTGrid = MathUtils.Max(minTGrid, child.PrevObject.TGrid);
                    var childMaxTGrid = MathUtils.Min(maxTGrid, child.TGrid);

                    ProcessConnectable(child, childMinTGrid, childMaxTGrid);
                }
            }

            using var d = lbk.GetAffactableWallLanes(EditorViewModel.Fumen).ToListWithObjectPool(out var list);
            var beginTGrid = lbk.TGrid;
            var endTGrid = lbk.EndIndicator.TGrid;
            var isNext = false;
            foreach (var start in list)
            {
                if (isNext)
                    PostPointByXTGrid(start.XGrid, start.TGrid, false);
                ProcessWallLane(start, beginTGrid, endTGrid);
                isNext = true;
            }

            NotifyOfPropertyChange(() => Lines);
            LineBrush = lbk.Direction == BlockDirection.Left ? WallLeftConnector.DefaultBrush : WallRightConnector.DefaultBrush;
        }

        private void O_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(TGrid):
                case nameof(XGrid):
                    RebuildLines();
                    break;
                default:
                    break;
            }
        }

        public override void OnEditorRedrawObjects()
        {
            RebuildLines();
            base.OnEditorRedrawObjects();
        }
    }
}
