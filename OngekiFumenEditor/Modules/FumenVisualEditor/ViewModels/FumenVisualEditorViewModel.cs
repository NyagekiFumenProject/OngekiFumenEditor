using Caliburn.Micro;
using Gemini.Framework;
using Gemini.Framework.Threading;
using Gemini.Modules.Toolbox;
using Gemini.Modules.Toolbox.Models;
using Gemini.Modules.Toolbox.Services;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Base.OngekiObjects.Beam;
using OngekiFumenEditor.Modules.FumenBulletPalleteListViewer;
using OngekiFumenEditor.Modules.FumenMetaInfoBrowser;
using OngekiFumenEditor.Modules.FumenObjectPropertyBrowser;
using OngekiFumenEditor.Modules.FumenVisualEditor.Base;
using OngekiFumenEditor.Modules.FumenVisualEditor.Controls;
using OngekiFumenEditor.Modules.FumenVisualEditor.Controls.OngekiObjects;
using OngekiFumenEditor.Modules.FumenVisualEditor.Models;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels.Dialogs;
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
        public IEnumerable<DisplayObjectViewModelBase> SelectObjects => EditorViewModels.OfType<DisplayObjectViewModelBase>().Where(x => x.IsSelected);

        [Flags]
        public enum RedrawTarget
        {
            OngekiObjects = 1,
            TGridUnitLines = 2,
            XGridUnitLines = 4,

            All = OngekiObjects | TGridUnitLines | XGridUnitLines,
            UnitLines = TGridUnitLines | XGridUnitLines,
        }

        private EditorProjectDataModel editorProjectData = new EditorProjectDataModel();
        public EditorProjectDataModel EditorProjectData
        {
            get
            {
                return editorProjectData;
            }
            set
            {
                Set(ref editorProjectData, value);
                Fumen = EditorProjectData.Fumen;
                NotifyOfPropertyChange(() => Setting);
            }
        }

        public OngekiFumen Fumen
        {
            get
            {
                return EditorProjectData.Fumen;
            }
            set
            {
                if (EditorProjectData.Fumen is not null)
                    EditorProjectData.Fumen.BpmList.OnChangedEvent -= OnBPMListChanged;
                if (value is not null)
                    value.BpmList.OnChangedEvent += OnBPMListChanged;
                EditorProjectData.Fumen = value;
                OnFumenObjectLoaded();
                Redraw(RedrawTarget.All);
                NotifyOfPropertyChange(() => Fumen);
            }
        }

        private double scrollViewerActualHeight;
        public double ScrollViewerActualHeight
        {
            get => scrollViewerActualHeight;
            set
            {
                Set(ref scrollViewerActualHeight, value);
            }
        }

        private double startVisibleCanvasY;
        public double StartVisibleCanvasY
        {
            get => startVisibleCanvasY;
            set
            {
                Set(ref startVisibleCanvasY, value);
            }
        }

        private double endVisibleCanvasY;
        public double EndVisibleCanvasY
        {
            get => endVisibleCanvasY;
            set
            {
                Set(ref endVisibleCanvasY, value);
            }
        }

        private double scrollViewerVerticalOffset;
        public double ScrollViewerVerticalOffset
        {
            get => scrollViewerVerticalOffset;
            set
            {
                Set(ref scrollViewerVerticalOffset, value);
                StartVisibleCanvasY = CanvasHeight - (ScrollViewerVerticalOffset + ScrollViewerActualHeight);
                EndVisibleCanvasY = CanvasHeight - ScrollViewerVerticalOffset;
            }
        }

        public double XUnitSize => CanvasWidth / (Setting.XGridMaxUnit * 2) * Setting.UnitCloseSize;
        public double CanvasWidth => View?.VisualDisplayer?.ActualWidth ?? 0;
        public double CanvasHeight => View?.VisualDisplayer?.ActualHeight ?? 0;
        public FumenVisualEditorView View { get; private set; }
        public ObservableCollection<XGridUnitLineViewModel> XGridUnitLineLocations { get; } = new();
        public ObservableCollection<TGridUnitLineViewModel> TGridUnitLineLocations { get; } = new();
        //public ItemCollection DisplayObjectList => View?.DisplayObjectList.Items;
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

        public EditorSetting Setting
        {
            get
            {
                return EditorProjectData.EditorSetting;
            }
            set
            {
                this.RegisterOrUnregisterPropertyChangeEvent(EditorProjectData.EditorSetting, value, OnSettingPropertyChanged);
                EditorProjectData.EditorSetting = value;
                NotifyOfPropertyChange(() => Setting);
            }
        }

        public FumenVisualEditorViewModel()
        {
            Setting = new EditorSetting();
        }

        public ObservableCollection<object> EditorViewModels { get; } = new();

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
                case nameof(EditorSetting.XGridMaxUnit):
                    Redraw(RedrawTarget.OngekiObjects | RedrawTarget.XGridUnitLines);
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
            using var _ = StatusNotifyHelper.BeginStatus("Editor project file loading : " + filePath);
            Log.LogInfo($"FumenVisualEditorViewModel DoLoad() : {filePath}");
            var projectData = await EditorProjectDataUtils.TryLoadFromFileAsync(filePath);
            EditorProjectData = projectData;
            Redraw(RedrawTarget.All);
        }


        private void OnFumenObjectLoaded()
        {
            IoC.Get<IFumenMetaInfoBrowser>().Fumen = Fumen;
            IoC.Get<IFumenBulletPalleteListViewer>().Fumen = Fumen;
        }

        private void RedrawTimeline()
        {
            foreach (var item in TGridUnitLineLocations)
                ObjectPool<TGridUnitLineViewModel>.Return(item);
            TGridUnitLineLocations.Clear();
            var baseLineAdded = false;

            foreach ((_, var bpm) in TGridCalculator.GetAllBpmUniformPositionList(this))
            {
                var nextBpm = Fumen.BpmList.GetNextBpm(bpm);
                var per = bpm.TGrid.ResT / Setting.BeatSplit;
                var i = 0;
                while (true)
                {
                    var tGrid = bpm.TGrid + new GridOffset(0, (int)(per * i));
                    if (nextBpm is not null && tGrid >= nextBpm.TGrid)
                        break;
                    var y = TGridCalculator.ConvertTGridToY(tGrid, this);
                    if (y > CanvasHeight)
                        break;
                    var line = ObjectPool<TGridUnitLineViewModel>.Get();
                    line.TGrid = tGrid;
                    line.IsBaseLine = tGrid == Setting.CurrentDisplayTimePosition;
                    line.Y = CanvasHeight - y;

                    baseLineAdded = baseLineAdded || line.IsBaseLine;
                    TGridUnitLineLocations.Add(line);
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
            if (Fumen is null || CanvasHeight == 0)
                return;
            var begin = TGridCalculator.ConvertYToTGrid(0, this) ?? new TGrid(0, 0);
            var end = TGridCalculator.ConvertYToTGrid(CanvasHeight, this);

            //Log.LogDebug($"begin:({begin})  end:({end})  base:({Setting.CurrentDisplayTimePosition})");
            foreach (var item in EditorViewModels.OfType<DisplayObjectViewModelBase>())
                item.RecaulateCanvasXY();
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

        protected override async Task DoNew()
        {
            var dialogViewModel = new EditorProjectSetupDialogViewModel();
            var result = await IoC.Get<IWindowManager>().ShowDialogAsync(dialogViewModel);
            if (result != true)
            {
                Log.LogInfo($"用户无法完成新建项目向导，关闭此编辑器");
                await TryCloseAsync(false);
                return;
            }
            EditorProjectData = dialogViewModel.EditorProjectData;
            Redraw(RedrawTarget.All);
            Log.LogInfo($"FumenVisualEditorViewModel DoNew()");
        }

        protected override async Task DoSave(string filePath)
        {
            using var _ = StatusNotifyHelper.BeginStatus("Fumen saving : " + filePath);
            Log.LogInfo($"FumenVisualEditorViewModel DoSave() : {filePath}");
            await EditorProjectDataUtils.TrySaveToFileAsync(filePath, EditorProjectData);
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
            Fumen.AddObject(viewModel.ReferenceOngekiObject);

            EditorViewModels.Add(viewModel);
            viewModel.EditorViewModel = this;
            //Redraw(RedrawTarget.OngekiObjects);

            //Log.LogInfo($"create new display object: {viewModel.ReferenceOngekiObject.GetType().Name}");
        }

        public void CopySelectedObjects()
        {

        }

        public void PasteCopiesObjects()
        {

        }

        public void OnFocusableChanged(ActionExecutionContext e)
        {
            Log.LogInfo($"OnFocusableChanged {e.EventArgs}");
        }

        public void OnBPMListChanged()
        {
            Redraw(RedrawTarget.TGridUnitLines);
        }

        public void Redraw(RedrawTarget target)
        {
            if (target.HasFlag(RedrawTarget.TGridUnitLines))
                RedrawTimeline();
            if (target.HasFlag(RedrawTarget.XGridUnitLines))
                RedrawUnitCloseXLines();
            if (target.HasFlag(RedrawTarget.OngekiObjects))
                RedrawEditorObjects();
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
                if (!Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
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

        #region Keyboard Actions

        public void KeyboardAction_DeleteSelectingObjects()
        {
            //删除已选择的物件
            var selectedObject = SelectObjects.ToArray();
            var propertyBrowser = IoC.Get<IFumenObjectPropertyBrowser>();

            foreach (var obj in selectedObject)
            {
                EditorViewModels.Remove(obj);
                Fumen.RemoveObject(obj.ReferenceOngekiObject);
                if (propertyBrowser != null && propertyBrowser.OngekiObject == obj.ReferenceOngekiObject)
                    propertyBrowser.OngekiObject = default;
            }
            Redraw(RedrawTarget.OngekiObjects);
            //Log.LogInfo($"deleted {selectedObject.Length} objects.");
        }

        public void KeyboardAction_SelectAllObjects()
        {
            EditorViewModels.OfType<DisplayObjectViewModelBase>().ForEach(x => x.IsSelected = true);
        }

        public void KeyboardAction_CancelSelectingObjects()
        {
            //取消选择
            SelectObjects.ForEach(x => x.IsSelected = false);
        }

        #endregion

        #region Drag Actions

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
                SelectObjects.ToArray().ForEach(x => x.OnDragEnd(pos));

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
                    vm.OnDragMoving(pos);
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
                    SelectObjects.ToArray().ForEach(x => dragCall(x, pos));
                }
            }
            else
            {
                SelectObjects.ToArray().ForEach(x => dragCall(x, pos));
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
            /*
                        if (displayObject is IEditorDisplayableViewModel editorObjectViewModel)
                            editorObjectViewModel.OnObjectCreated(displayObject.ReferenceOngekiObject, this);
            */
            OnNewObjectAdd(displayObject);
            displayObject.MoveCanvas(mousePosition);
            Redraw(RedrawTarget.OngekiObjects);
        }

        #endregion
    }
}
