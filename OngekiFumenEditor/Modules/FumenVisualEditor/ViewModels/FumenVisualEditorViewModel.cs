using Caliburn.Micro;
using Gemini.Framework;
using Gemini.Modules.Toolbox.Services;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Modules.FumenMetaInfoBrowser;
using OngekiFumenEditor.Modules.FumenVisualEditor.Controls;
using OngekiFumenEditor.Modules.FumenVisualEditor.Controls.OngekiObjects;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels.OngekiObjects;
using OngekiFumenEditor.Modules.FumenVisualEditor.Views;
using OngekiFumenEditor.Parser;
using OngekiFumenEditor.Utils;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels
{
    [Export(typeof(FumenVisualEditorViewModel))]
    public class FumenVisualEditorViewModel : PersistedDocument
    {
        public struct XGridUnitLineViewModel
        {
            public double X { get; set; }
            public double Unit { get; set; }
            public bool IsCenterLine { get; set; }
            public override string ToString() => $"{X:F4} {Unit} {(IsCenterLine ? "Center" : string.Empty)}";
        }

        public struct TGridUnitLineViewModel
        {
            public double Y { get; set; }
            public TGrid TGrid { get; set; }
            public bool IsBaseLine { get; set; }
            public override string ToString() => $"{Y:F4} {TGrid} {(IsBaseLine ? "BaseLine" : string.Empty)}";
        }

        private OngekiFumen fumen;
        public OngekiFumen Fumen
        {
            get
            {
                return fumen;
            }
            set
            {
                fumen = value;
                OnFumenObjectLoaded();
                RedrawTimeline();
                NotifyOfPropertyChange(() => Fumen);
            }
        }

        public double XUnitSize => CanvasWidth / (24 * 2) * UnitCloseSize;
        public double CanvasWidth => VisualDisplayer?.ActualWidth ?? 0;
        public double CanvasHeight => VisualDisplayer?.ActualHeight ?? 0;
        public FumenVisualEditorView View { get; private set; }
        public ObservableCollection<XGridUnitLineViewModel> XGridUnitLineLocations { get; } = new();
        public ObservableCollection<TGridUnitLineViewModel> TGridUnitLineLocations { get; } = new();
        public Panel VisualDisplayer => View?.VisualDisplayer;
        public ItemCollection DisplayObjectList => View?.DisplayObjectList.Items;

        private string errorMessage;
        public string ErrorMessage
        {
            get
            {
                return errorMessage;
            }
            set
            {
                errorMessage = value;
                if (!string.IsNullOrWhiteSpace(value))
                    Log.LogError("Current error message : " + value);
                NotifyOfPropertyChange(() => ErrorMessage);
            }
        }

        private TGrid currentDisplayTimePosition = new TGrid();
        /// <summary>
        /// 表示当前显示物件的时间
        /// </summary>
        public TGrid CurrentDisplayTimePosition
        {
            get
            {
                return currentDisplayTimePosition;
            }
            set
            {
                currentDisplayTimePosition = value;
                NotifyOfPropertyChange(() => CurrentDisplayTimePosition);
            }
        }

        public override string DisplayName
        {
            get { return base.DisplayName; }
            set
            {
                base.DisplayName = value;
                if (IoC.Get<WindowTitleHelper>() is WindowTitleHelper title)
                {
                    title.TitleContent = base.DisplayName;
                }
            }
        }

        private bool isPreventXAutoClose;
        public bool IsPreventXAutoClose
        {
            get
            {
                return isPreventXAutoClose;
            }
            set
            {
                isPreventXAutoClose = value;
                NotifyOfPropertyChange(() => IsPreventTimelineAutoClose);
            }
        }

        private bool isPreventTimelineAutoClose;
        public bool IsPreventTimelineAutoClose
        {
            get
            {
                return isPreventTimelineAutoClose;
            }
            set
            {
                isPreventTimelineAutoClose = value;
                NotifyOfPropertyChange(() => IsPreventTimelineAutoClose);
            }
        }

        private double unitCloseSize = 4;
        public double UnitCloseSize
        {
            get
            {
                return unitCloseSize;
            }
            set
            {
                unitCloseSize = value;
                RedrawUnitCloseXLines();
                NotifyOfPropertyChange(() => UnitCloseSize);
            }
        }

        private int baseLineY = 50;
        public int BaseLineY
        {
            get
            {
                return baseLineY;
            }
            set
            {
                baseLineY = value;
                RedrawTimeline();
                NotifyOfPropertyChange(() => BaseLineY);
            }
        }

        private int beatSplit = 4;
        public int BeatSplit
        {
            get
            {
                return beatSplit;
            }
            set
            {
                beatSplit = value;
                RedrawTimeline();
                NotifyOfPropertyChange(() => BeatSplit);
            }
        }

        private void RedrawUnitCloseXLines()
        {
            XGridUnitLineLocations.Clear();

            var width = CanvasWidth;
            var unitSize = XUnitSize;
            var totalUnitValue = 0d;

            for (double totalLength = width / 2 + unitSize; totalLength < width; totalLength += unitSize)
            {
                totalUnitValue += UnitCloseSize;

                XGridUnitLineLocations.Add(new XGridUnitLineViewModel()
                {
                    X = totalLength,
                    Unit = totalUnitValue,
                    IsCenterLine = false
                });
                XGridUnitLineLocations.Add(new XGridUnitLineViewModel()
                {
                    X = (width / 2) - (totalLength - (width / 2)),
                    Unit = -totalUnitValue,
                    IsCenterLine = false
                });
            }
            XGridUnitLineLocations.Add(new XGridUnitLineViewModel()
            {
                X = width / 2,
                IsCenterLine = true
            });
        }

        protected override void OnViewLoaded(object v)
        {
            base.OnViewLoaded(v);
            var view = v as FumenVisualEditorView;

            View = view;
            RedrawUnitCloseXLines();
        }

        private Task InitalizeVisualData()
        {
            var displayableObjects = fumen.GetAllDisplayableObjects();
            //add all displayable object.
            foreach (var obj in displayableObjects)
            {
                var displayObject = Activator.CreateInstance(obj.ModelViewType) as DisplayObjectViewModelBase;
                if (ViewHelper.CreateView(displayObject) is OngekiObjectViewBase view && obj is OngekiObjectBase o)
                {
                    view.ViewModel.ReferenceOngekiObject = o;
                    view.ViewModel.EditorViewModel = this;
                    DisplayObjectList.Add(view);
                }
            }
            return Task.CompletedTask;
        }

        protected override async Task DoLoad(string filePath)
        {
            using var _ = StatusNotifyHelper.BeginStatus("Fumen loading : " + filePath);
            Log.LogInfo($"FumenVisualEditorViewModel DoLoad() : {filePath}");
            using var fileStream = File.OpenRead(filePath);
            Fumen = await IoC.Get<IOngekiFumenParser>().ParseAsync(fileStream);
            await InitalizeVisualData();
        }

        private void OnFumenObjectLoaded()
        {
            IoC.Get<IFumenMetaInfoBrowser>().Fumen = Fumen;
        }

        private void RedrawTimeline()
        {
            TGridUnitLineLocations.Clear();
            foreach ((_, var bpm) in TGridCalculator.GetVisibleBpmList(this))
            {
                var nextBpm = Fumen.BpmList.GetNextBpm(bpm);
                var per = bpm.TGrid.ResT / BeatSplit;
                var i = 0;
                while (true)
                {
                    var tGrid = bpm.TGrid + new GridOffset(0, (int)(per * i));
                    if (nextBpm is not null && tGrid >= nextBpm.TGrid)
                        break;
                    if (TGridCalculator.ConvertTGridToY(tGrid, this) is double y)
                    {
                        if (y > CanvasHeight)
                            break;
                        var line = new TGridUnitLineViewModel()
                        {
                            TGrid = tGrid,
                            IsBaseLine = tGrid == this.CurrentDisplayTimePosition,
                            Y = CanvasHeight - y
                        };
                        TGridUnitLineLocations.Add(line);
                    }
                    i++;
                }
            }
        }

        protected override async Task DoNew()
        {
            Fumen = new OngekiFumen();
            await InitalizeVisualData();
            Log.LogInfo($"FumenVisualEditorViewModel DoNew()");
        }

        protected override async Task DoSave(string filePath)
        {
            using var _ = StatusNotifyHelper.BeginStatus("Fumen saving : " + filePath);
            await File.WriteAllTextAsync(filePath, fumen.Serialize());
            Log.LogInfo($"FumenVisualEditorViewModel DoSave() : {filePath}");
        }

        public void OnNewObjectAdd(DisplayObjectViewModelBase viewModel)
        {
            var view = ViewHelper.CreateView(viewModel);
            fumen.AddObject(viewModel.ReferenceOngekiObject);

            DisplayObjectList.Add(view);
            viewModel.EditorViewModel = this;

            Log.LogInfo($"create new display object: {viewModel.ReferenceOngekiObject.GetType().Name}");
        }

        public void DeleteSelectedObjects()
        {
            var selectedObject = DisplayObjectList.OfType<OngekiObjectViewBase>().Where(x => x.IsSelected).ToArray();
            foreach (var obj in selectedObject)
            {
                DisplayObjectList.Remove(obj);
                fumen.AddObject(obj.ViewModel?.ReferenceOngekiObject);
            }
            Log.LogInfo($"deleted {selectedObject.Length} objects.");
        }

        public void CopySelectedObjects()
        {

        }

        public void PasteCopiesObjects()
        {

        }

        public void OnKeyDown(ActionExecutionContext e)
        {
            if (e.EventArgs is KeyEventArgs arg)
            {
                if (arg.Key == Key.Delete)
                {
                    DeleteSelectedObjects();
                }
            }
        }

        public void OnSizeChanged(ActionExecutionContext e)
        {
            //redraw visual editor and ongeki objects.
            RedrawUnitCloseXLines();
            RedrawTimeline();
            foreach (var obj in DisplayObjectList.OfType<OngekiObjectViewBase>())
                obj.RecalcCanvasXY();
        }
    }
}
