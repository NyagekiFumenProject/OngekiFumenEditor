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
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels.EditorObjects;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels.OngekiObjects;
using OngekiFumenEditor.Modules.FumenVisualEditor.Views;
using OngekiFumenEditor.Modules.FumenVisualEditorSettings;
using OngekiFumenEditor.Parser;
using OngekiFumenEditor.UI.Controls;
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
    public partial class FumenVisualEditorViewModel : PersistedDocument
    {
        public ObservableCollection<IEditorDisplayableViewModel> CurrentDisplayEditorViewModels { get; } = new();

        private double xUnitSize = default;
        public double XUnitSize
        {
            get => xUnitSize;
            set => Set(ref xUnitSize, value);
        }

        protected void RecalculateXUnitSize()
        {
            XUnitSize = CanvasWidth / (Setting.XGridMaxUnit * 2) * Setting.UnitCloseSize;
        }

        protected override void OnViewLoaded(object v)
        {
            base.OnViewLoaded(v);
            RedrawUnitCloseXLines();
        }

        private void RedrawTimeline()
        {
            foreach (var item in TGridUnitLineLocations)
                ObjectPool<TGridUnitLineViewModel>.Return(item);
            TGridUnitLineLocations.Clear();
            var baseLineAdded = false;

            var bpmList = Fumen.BpmList;
            var endTGrid = TGridCalculator.ConvertYToTGrid(TotalDurationHeight, this);
            var beginBpm = bpmList.FirstBpm;
            var currentBpm = beginBpm;
            var currentTGridBase = new TGrid(0,0);
            while (currentBpm is not null)
            {
                var nextBpm = Fumen.BpmList.GetNextBpm(currentBpm);
                var per = currentBpm.TGrid.ResT / Setting.BeatSplit;
                var diff = currentTGridBase - currentBpm.TGrid;
                var totalGrid = diff.Unit * currentBpm.TGrid.ResT + diff.Grid;
                var i = (int)Math.Max(0, totalGrid / per);
                while (true)
                {
                    var tGrid = currentBpm.TGrid + new GridOffset(0, (int)(per * i));
                    if (nextBpm is not null && tGrid >= nextBpm.TGrid)
                        break;
                    if (tGrid > endTGrid)
                        return;
                    var y = TGridCalculator.ConvertTGridToY(tGrid, this);
                    var line = ObjectPool<TGridUnitLineViewModel>.Get();
                    line.TGrid = tGrid;
                    line.Y = TotalDurationHeight - y;
                    line.BeatRhythm = i;
                    baseLineAdded = baseLineAdded || line.IsBaseLine;
                    TGridUnitLineLocations.Add(line);
                    i++;
                }
                currentBpm = nextBpm;
                currentTGridBase = nextBpm.TGrid;
            }
        }

        private void RedrawEditorObjects()
        {
            if (Fumen is null || CanvasHeight == 0)
                return;
            //Log.LogDebug($"begin");
            var min = TGridCalculator.ConvertYToTGrid(MinVisibleCanvasY, this) ?? new TGrid(0, 0);
            var max = TGridCalculator.ConvertYToTGrid(MaxVisibleCanvasY, this);

            //Log.LogDebug($"begin:({begin})  end:({end})  base:({Setting.CurrentDisplayTimePosition})");
            using var d = ObjectPool<HashSet<IDisplayableObject>>.GetWithUsingDisposable(out var currentDisplayingObjects, out var _);
            currentDisplayingObjects.Clear();
            using var d2 = ObjectPool<HashSet<IDisplayableObject>>.GetWithUsingDisposable(out var allDisplayableObjects, out var _);
            allDisplayableObjects.Clear();
            using var d3 = ObjectPool<HashSet<IEditorDisplayableViewModel>>.GetWithUsingDisposable(out var removeObjects, out var _);
            removeObjects.Clear();

            Fumen.GetAllDisplayableObjects().ForEach(x => allDisplayableObjects.Add(x));

            EditorViewModels
                .OfType<IEditorDisplayableViewModel>()
                .ForEach(x => currentDisplayingObjects.Add(x.DisplayableObject));

            //检查当前显示的物件是否还在谱面中，不在就删除，在就更新位置
            foreach (var viewModel in EditorViewModels.OfType<IEditorDisplayableViewModel>())
            {
                var refObject = viewModel.DisplayableObject;
                //检查是否还存在
                if (!allDisplayableObjects.Contains(refObject))
                    removeObjects.Add(viewModel);
            }
            foreach (var removeViewModel in removeObjects)
                EditorViewModels.Remove(removeViewModel);

            //将还没显示的都塞进去显示了
            var c = 0;
            foreach (var add in allDisplayableObjects
                .Where(x => !currentDisplayingObjects.Contains(x)))
            {
                currentDisplayingObjects.Add(add);
                var viewModel = CacheLambdaActivator.CreateInstance(add.ModelViewType) as IEditorDisplayableViewModel;
                viewModel.OnObjectCreated(add, this);
                EditorViewModels.Add(viewModel);
                //odLog.LogDebug($"add viewmodel : {add}");
                c++;
            }

            removeObjects.Clear();//复用
            foreach (var currentDisplaying in CurrentDisplayEditorViewModels)
            {
                if (!currentDisplaying.DisplayableObject.CheckVisiable(min, max))
                {
                    //remove
                    removeObjects.Add(currentDisplaying);
                }
            }
            removeObjects.ForEach(x => CurrentDisplayEditorViewModels.Remove(x));
            CurrentDisplayEditorViewModels.AddRange(EditorViewModels.Where(x => x.DisplayableObject.CheckVisiable(min, max) && !CurrentDisplayEditorViewModels.Contains(x)));

            foreach (var viewModel in CurrentDisplayEditorViewModels)
            {
                if (viewModel is IEditorDisplayableViewModel editorViewModel)
                    editorViewModel.OnEditorRedrawObjects();
            }

            //Log.LogDebug($"removed {removeObjects.Count} objects , added {c} objects, displaying {CurrentDisplayEditorViewModels.Count} objects.");
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

        public void Redraw(RedrawTarget target)
        {
            if (target.HasFlag(RedrawTarget.TGridUnitLines))
                RedrawTimeline();
            if (target.HasFlag(RedrawTarget.XGridUnitLines))
                RedrawUnitCloseXLines();
            if (target.HasFlag(RedrawTarget.OngekiObjects))
                RedrawEditorObjects();
            if (target.HasFlag(RedrawTarget.ScrollBar))
                RecalculateScrollBar();
        }

        public void OnLoaded(ActionExecutionContext e)
        {
            var scrollViewer = e.Source as AnimatedScrollViewer;
            var view = e.View as FrameworkElement;
            CanvasWidth = scrollViewer.ViewportWidth;
            CanvasHeight = view.ActualHeight;
            Redraw(RedrawTarget.All);
        }

        public void OnSizeChanged(ActionExecutionContext e)
        {
            var scrollViewer = e.Source as AnimatedScrollViewer;
            var arg = e.EventArgs as SizeChangedEventArgs;
            CanvasWidth = scrollViewer.ViewportWidth;
            CanvasHeight = arg.NewSize.Height;
            Redraw(RedrawTarget.All);
        }
    }
}
