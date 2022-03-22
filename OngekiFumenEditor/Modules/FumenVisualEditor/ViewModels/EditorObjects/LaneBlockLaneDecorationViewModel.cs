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

namespace OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels.EditorObjects
{
    public class BindableDynmaticXGridTGridSegement : BindableTGridSegement
    {
        private TGrid pinTGrid;
        public TGrid PinTGrid
        {
            get => pinTGrid;
            set
            {
                Set(ref pinTGrid, value);
            }
        }

        private ConnectableStartObject bindObject;
        public ConnectableStartObject BindObject
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

        public override bool RecalcPoint()
        {
            if (BindObject is null || BindFumenEditor is null || BindObject.CalulateXGrid(PinTGrid) is not XGrid xGrid)
                return false;

            var x = XGridCalculator.ConvertXGridToX(xGrid, BindFumenEditor)
                + 0.0000001 * MathUtils.Random(100);
            var y = BindFumenEditor.TotalDurationHeight - TGridCalculator.ConvertTGridToY(PinTGrid, BindFumenEditor);

            Segment.Point = new Point(x, y);
            return true;
        }
    }

    [MapToView(ViewType = typeof(HoldConnectorView))]
    public class LaneBlockLaneDecorationViewModel : ConnectorViewModel
    {
        private Dictionary<object, BindableDynmaticXGridTGridSegement> map = new();
        public PathSegmentCollection Lines { get; } = new();

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

        public Brush LineBrush => Brushes.DeepPink;

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
            //Log.LogDebug($"----------");

            void Upsert(ConnectableStartObject obj, TGrid pinTGrid, object key)
            {
                if (map.TryGetValue(key, out var seg))
                {
                    seg.BindObject = obj;
                    seg.PinTGrid = pinTGrid;
                    if (seg.RecalcPoint())
                    {
                        //Log.LogDebug($"{pinTGrid} {key}");
                        Lines.Add(seg.Segment);
                    }
                    return;
                }
                var bind = new BindableDynmaticXGridTGridSegement()
                {
                    BindFumenEditor = EditorViewModel,
                    BindObject = obj,
                    PinTGrid = pinTGrid
                };
                map[key] = bind;
                if (bind.RecalcPoint())
                {
                    //Log.LogDebug($"{pinTGrid} {key}");
                    Lines.Add(bind.Segment);
                }
                //Log.LogDebug("new");
            }

            using var d = lbk.GetAffactableWallLanes(EditorViewModel.Fumen).ToListWithObjectPool(out var list);
            var beginTGrid = lbk.TGrid;
            var endTGrid = lbk.EndIndicator.TGrid;

            Upsert(list.FirstOrDefault(), beginTGrid, lbk);
            foreach ((var node, var tGrid, var key) in list
                .SelectMany(x => x.Children.AsEnumerable<ConnectableObjectBase>()
                    .Prepend(x)
                    .Where(x => beginTGrid <= x.TGrid && x.TGrid <= endTGrid)
                    .Select(z => (x, z.TGrid, z))))
                Upsert(node, tGrid, key);
            if (list.LastOrDefault() is LaneStartBase laneStart && laneStart.MaxTGrid >= endTGrid)
                Upsert(list.LastOrDefault(), endTGrid, lbkEnd);

            NotifyOfPropertyChange(() => Lines);
        }

        public override void OnEditorRedrawObjects()
        {
            RebuildLines();
            base.OnEditorRedrawObjects();
        }
    }
}
