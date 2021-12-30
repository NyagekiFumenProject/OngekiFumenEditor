using Caliburn.Micro;
using Gemini.Framework;
using Gemini.Framework.Threading;
using Gemini.Modules.Toolbox;
using Gemini.Modules.Toolbox.Models;
using Gemini.Modules.Toolbox.Services;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Base.OngekiObjects.Beam;
using OngekiFumenEditor.Modules.FumenMetaInfoBrowser;
using OngekiFumenEditor.Modules.FumenObjectPropertyBrowser;
using OngekiFumenEditor.Modules.FumenVisualEditor.Base;
using OngekiFumenEditor.Modules.FumenVisualEditor.Controls;
using OngekiFumenEditor.Modules.FumenVisualEditor.Controls.OngekiObjects;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels.OngekiObjects;
using OngekiFumenEditor.Modules.FumenVisualEditor.Views;
using OngekiFumenEditor.Modules.FumenVisualEditorSettings;
using OngekiFumenEditor.Parser;
using OngekiFumenEditor.Utils;
using OngekiFumenEditor.Utils.ObjectPool;
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
using System.Windows.Media;
using System.Windows.Threading;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels
{
    [Export(typeof(FumenVisualEditorViewModel))]
    public class FumenVisualEditorViewModel : PersistedDocument
    {
        public IEnumerable<DisplayObjectViewModelBase> SelectObjects => DisplayObjectList.OfType<OngekiObjectViewBase>().Select(x => x.ViewModel).Where(x => x.IsSelected);

        [Flags]
        public enum RedrawTarget
        {
            OngekiObjects = 1,
            TGridUnitLines = 2,
            XGridUnitLines = 4,

            All = OngekiObjects | TGridUnitLines | XGridUnitLines,
            UnitLines = TGridUnitLines | XGridUnitLines,
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
                if (fumen is not null)
                    fumen.BpmList.OnChangedEvent -= OnBPMListChanged;
                if (value is not null)
                    value.BpmList.OnChangedEvent += OnBPMListChanged;
                fumen = value;
                OnFumenObjectLoaded();
                Redraw(RedrawTarget.All);
                NotifyOfPropertyChange(() => Fumen);
            }
        }

        public double XUnitSize => CanvasWidth / (24 * 2) * Setting.UnitCloseSize;
        public double CanvasWidth => View?.VisualDisplayer?.ActualWidth ?? 0;
        public double CanvasHeight => View?.VisualDisplayer?.ActualHeight ?? 0;
        public FumenVisualEditorView View { get; private set; }
        public ObservableCollection<XGridUnitLineViewModel> XGridUnitLineLocations { get; } = new();
        public ObservableCollection<TGridUnitLineViewModel> TGridUnitLineLocations { get; } = new();
        public ItemCollection DisplayObjectList => View?.DisplayObjectList.Items;
        public bool isDragging;
        public bool isMouseDown;

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

        private EditorSetting setting;
        private bool shiftKeyDown;

        public EditorSetting Setting
        {
            get
            {
                return setting;
            }
            set
            {
                this.RegisterOrUnregisterPropertyChangeEvent(setting, value, OnSettingPropertyChanged);
                setting = value;
                NotifyOfPropertyChange(() => Setting);
            }
        }

        public FumenVisualEditorViewModel()
        {
            Setting = new EditorSetting();
        }

        private void OnSettingPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(EditorSetting.UnitCloseSize):
                    Redraw(RedrawTarget.XGridUnitLines);
                    break;
                case nameof(EditorSetting.CurrentDisplayTimePosition):
                    Redraw(RedrawTarget.TGridUnitLines | RedrawTarget.OngekiObjects);
                    break;
                case nameof(EditorSetting.BeatSplit):
                case nameof(EditorSetting.BaseLineY):
                    Redraw(RedrawTarget.TGridUnitLines);
                    break;
                case nameof(EditorSetting.EditorDisplayName):
                    if (IoC.Get<WindowTitleHelper>() is WindowTitleHelper title)
                        title.TitleContent = base.DisplayName;
                    break;
                default:
                    break;
            }
        }

        protected override void OnViewLoaded(object v)
        {
            base.OnViewLoaded(v);
            var view = v as FumenVisualEditorView;

            View = view;
            RedrawUnitCloseXLines();
        }

        protected override async Task DoLoad(string filePath)
        {
            using var _ = StatusNotifyHelper.BeginStatus("Fumen loading : " + filePath);
            Log.LogInfo($"FumenVisualEditorViewModel DoLoad() : {filePath}");
            using var fileStream = File.OpenRead(filePath);
            Fumen = await IoC.Get<IOngekiFumenParser>().ParseAsync(fileStream);
            Redraw(RedrawTarget.All);
        }

        private void OnFumenObjectLoaded()
        {
            IoC.Get<IFumenMetaInfoBrowser>().Fumen = Fumen;
        }

        private void RedrawTimeline()
        {
            foreach (var item in TGridUnitLineLocations)
                ObjectPool<TGridUnitLineViewModel>.Return(item);
            TGridUnitLineLocations.Clear();
            var baseLineAdded = false;

            foreach ((_, var bpm) in TGridCalculator.GetVisibleBpmList(this))
            {
                var nextBpm = Fumen.BpmList.GetNextBpm(bpm);
                var per = bpm.TGrid.ResT / Setting.BeatSplit;
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
                        var line = ObjectPool<TGridUnitLineViewModel>.Get();
                        line.TGrid = tGrid;
                        line.IsBaseLine = tGrid == Setting.CurrentDisplayTimePosition;
                        line.Y = CanvasHeight - y;

                        baseLineAdded = baseLineAdded || line.IsBaseLine;

                        TGridUnitLineLocations.Add(line);
                    }
                    i++;
                }
            }

            if (!baseLineAdded)
            {
                //添加一个基线表示当前时间轴
                if (TGridCalculator.ConvertTGridToY(Setting.CurrentDisplayTimePosition, this) is double y)
                {
                    var line = ObjectPool<TGridUnitLineViewModel>.Get();

                    line.TGrid = Setting.CurrentDisplayTimePosition;
                    line.IsBaseLine = true;
                    line.Y = CanvasHeight - y;

                    TGridUnitLineLocations.Add(line);
                }
            }
        }

        private void RedrawEditorObjects()
        {
            var begin = TGridCalculator.ConvertYToTGrid(0, this) ?? new TGrid(0, 0);
            var end = TGridCalculator.ConvertYToTGrid(CanvasHeight, this);

            //Log.LogDebug($"begin:({begin})  end:({end})");

            DisplayObjectList.Clear();
            var list = Fumen.GetAllDisplayableObjects()
                .OfType<IDisplayableObject>()
                .Where(x => x.CheckVisiable(begin, end))
                .ToArray();
            foreach (var item in list)
            {
                var obj = Activator.CreateInstance(item.ModelViewType);
                var view = ViewHelper.CreateView(obj);

                if (obj is IEditorDisplayableViewModel editorObjectViewModel)
                    editorObjectViewModel.OnObjectCreated(item, this);

                DisplayObjectList.Add(view);
            }
        }

        private void RedrawUnitCloseXLines()
        {
            foreach (var item in XGridUnitLineLocations)
                ObjectPool<XGridUnitLineViewModel>.Return(item);
            XGridUnitLineLocations.Clear();

            var width = CanvasWidth;
            var unitSize = XUnitSize;
            var totalUnitValue = 0d;
            var line = default(XGridUnitLineViewModel);

            for (double totalLength = width / 2 + unitSize; totalLength < width; totalLength += unitSize)
            {
                totalUnitValue += Setting.UnitCloseSize;

                line = ObjectPool<XGridUnitLineViewModel>.Get();
                line.X = totalLength;
                line.Unit = totalUnitValue;
                line.IsCenterLine = false;
                XGridUnitLineLocations.Add(line);

                line = ObjectPool<XGridUnitLineViewModel>.Get();
                line.X = (width / 2) - (totalLength - (width / 2));
                line.Unit = -totalUnitValue;
                line.IsCenterLine = false;
                XGridUnitLineLocations.Add(line);
            }

            line = ObjectPool<XGridUnitLineViewModel>.Get();
            line.X = width / 2;
            line.IsCenterLine = true;
            XGridUnitLineLocations.Add(line);
        }

        protected override Task DoNew()
        {
            Fumen = new OngekiFumen();
            Redraw(RedrawTarget.All);
            Log.LogInfo($"FumenVisualEditorViewModel DoNew()");
            return TaskUtility.Completed;
        }

        protected override async Task DoSave(string filePath)
        {
            using var _ = StatusNotifyHelper.BeginStatus("Fumen saving : " + filePath);
            await File.WriteAllTextAsync(filePath, fumen.Serialize());
            Log.LogInfo($"FumenVisualEditorViewModel DoSave() : {filePath}");
        }

        protected override Task OnActivateAsync(CancellationToken cancellationToken)
        {
            if (IoC.Get<IFumenVisualEditorSettings>() is IFumenVisualEditorSettings editorSettings)
                editorSettings.Setting = Setting;
            return base.OnActivateAsync(cancellationToken);
        }

        protected override Task OnDeactivateAsync(bool close, CancellationToken cancellationToken)
        {
            if (IoC.Get<IFumenVisualEditorSettings>() is IFumenVisualEditorSettings editorSettings && editorSettings.Setting == Setting)
                editorSettings.Setting = default;
            return base.OnDeactivateAsync(close, cancellationToken);
        }

        public void OnNewObjectAdd(DisplayObjectViewModelBase viewModel)
        {
            var view = ViewHelper.CreateView(viewModel);
            fumen.AddObject(viewModel.ReferenceOngekiObject);

            DisplayObjectList.Add(view);
            viewModel.EditorViewModel = this;
            //Redraw(RedrawTarget.OngekiObjects);

            //Log.LogInfo($"create new display object: {viewModel.ReferenceOngekiObject.GetType().Name}");
        }

        public void DeleteSelectedObjects()
        {
            var selectedObject = SelectObjects.ToArray();
            foreach (var obj in selectedObject)
            {
                DisplayObjectList.Remove(obj.View);
                fumen.RemoveObject(obj.ReferenceOngekiObject);
            }
            //Log.LogInfo($"deleted {selectedObject.Length} objects.");
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
                //Log.LogInfo(arg.Key.ToString());
                if (arg.Key == Key.Delete)
                {
                    DeleteSelectedObjects();
                }
                if (arg.Key == Key.LeftShift)
                {
                    shiftKeyDown = true;
                }
            }
        }

        public void OnKeyUp(ActionExecutionContext e)
        {
            if (e.EventArgs is KeyEventArgs arg)
            {
                //Log.LogInfo(arg.Key.ToString());
                if (arg.Key == Key.Delete)
                {
                    DeleteSelectedObjects();
                }
                if (arg.Key == Key.LeftShift)
                {
                    shiftKeyDown = false;
                }
            }
        }

        public void OnFocusableChanged(ActionExecutionContext e)
        {
            Log.LogInfo($"OnFocusableChanged {e.EventArgs}");
        }

        public void OnBPMListChanged()
        {
            RedrawTimeline();
        }

        public void Redraw(RedrawTarget target)
        {
            if (target.HasFlag(RedrawTarget.OngekiObjects))
                RedrawEditorObjects();
            if (target.HasFlag(RedrawTarget.TGridUnitLines))
                RedrawTimeline();
            if (target.HasFlag(RedrawTarget.XGridUnitLines))
                RedrawUnitCloseXLines();
        }

        public void OnSizeChanged(ActionExecutionContext e)
        {
            Redraw(RedrawTarget.All);
        }

        public void OnMouseWheel(ActionExecutionContext e)
        {
            var arg = e.EventArgs as MouseWheelEventArgs;
            var scrollDelta = (int)(arg.Delta * Setting.MouseWheelTimelineSpeed);
            var tGrid = Setting.CurrentDisplayTimePosition + new GridOffset(0, scrollDelta);

            if (tGrid < TGrid.ZeroDefault)
                tGrid = TGrid.ZeroDefault;

            Setting.CurrentDisplayTimePosition = tGrid;
        }

        internal void OnSelectPropertyChanged(DisplayObjectViewModelBase obj, bool value)
        {
            if (value)
            {
                if (!shiftKeyDown)
                {
                    foreach (var o in SelectObjects.Where(x => x != obj))
                        o.IsSelected = false;
                }
                if (IoC.Get<IFumenObjectPropertyBrowser>() is IFumenObjectPropertyBrowser propertyBrowser)
                    propertyBrowser.OngekiObject = SelectObjects.Count() == 1 ? SelectObjects.First().ReferenceOngekiObject : default;
            }
            else
            {
                if (IoC.Get<IFumenObjectPropertyBrowser>() is IFumenObjectPropertyBrowser propertyBrowser && propertyBrowser.OngekiObject == obj.ReferenceOngekiObject)
                    propertyBrowser.OngekiObject = default;
            }
        }

        #region DragActions

        public void OnMouseLeave(ActionExecutionContext e)
        {
            //Log.LogInfo("OnMouseLeave");
            if (!(isMouseDown && View.Parent is IInputElement parent))
                return;
            isMouseDown = false;
            isDragging = false;
            var pos = (e.EventArgs as MouseEventArgs).GetPosition(parent);
            SelectObjects.ForEach(x => x.OnDragEnd(pos));
            //e.Handled = true;
        }

        public void OnMouseUp(ActionExecutionContext e)
        {
            //Log.LogInfo("OnMouseUp");
            if (!(isMouseDown && View.Parent is IInputElement parent))
                return;

            var pos = (e.EventArgs as MouseEventArgs).GetPosition(parent);
            if (isDragging)
                SelectObjects.ForEach(x => x.OnDragEnd(pos));

            isMouseDown = false;
            isDragging = false;
            //e.Handled = true;
        }

        public void OnMouseMove(ActionExecutionContext e)
        {
            //Log.LogInfo("OnMouseMove");
            if (!(isMouseDown && View.Parent is IInputElement parent))
                return;
            //e.Handled = true;
            var r = isDragging;
            Action<DisplayObjectViewModelBase, Point> dragCall = (vm, pos) =>
            {
                if (r)
                    vm.OnDragEnd(pos);
                else
                    vm.OnDragStart(pos);
            };
            isDragging = true;

            var pos = (e.EventArgs as MouseEventArgs).GetPosition(parent);
            if (VisualTreeUtility.FindParent<Visual>(View) is FrameworkElement uiElement)
            {
                var bound = new Rect(0, 0, uiElement.ActualWidth, uiElement.ActualHeight);
                if (bound.Contains(pos))
                {
                    SelectObjects.ForEach(x => dragCall(x, pos));
                }
            }
            else
            {
                SelectObjects.ForEach(x => dragCall(x, pos));
            }
        }

        public void OnMouseDown(ActionExecutionContext e)
        {
            //Log.LogInfo("OnMouseDown");
            if ((e.EventArgs as MouseEventArgs).LeftButton == MouseButtonState.Pressed)
            {
                isMouseDown = true;
                isDragging = false;
                //e.Handled = true;
            }
            View.Focus();
        }

        public void Grid_DragEnter(ActionExecutionContext e)
        {
            var arg = e.EventArgs as DragEventArgs;
            if (!arg.Data.GetDataPresent(ToolboxDragDrop.DataFormat))
                arg.Effects = DragDropEffects.None;
        }

        public void Grid_Drop(ActionExecutionContext e)
        {
            var arg = e.EventArgs as DragEventArgs;
            if (!arg.Data.GetDataPresent(ToolboxDragDrop.DataFormat))
                return;

            var mousePosition = arg.GetPosition(View.VisualDisplayer);
            var displayObject = default(DisplayObjectViewModelBase);

            switch (arg.Data.GetData(ToolboxDragDrop.DataFormat))
            {
                case ToolboxItem toolboxItem:
                    displayObject = Activator.CreateInstance(toolboxItem.ItemType) as DisplayObjectViewModelBase;
                    break;
                case OngekiObjectDropParam dropParam:
                    displayObject = dropParam.OngekiObjectViewModel.Value;
                    break;
            }

            OnNewObjectAdd(displayObject);
            displayObject.MoveCanvas(mousePosition);
        }

        #endregion
    }
}
