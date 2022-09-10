using Caliburn.Micro;
using Gemini.Framework;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Modules.FumenVisualEditor.Base;
using OngekiFumenEditor.UI.Controls;
using OngekiFumenEditor.Utils;
using OngekiFumenEditor.Utils.ObjectPool;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels
{
    public partial class FumenVisualEditorViewModel : PersistedDocument
    {
        public ObservableCollection<IEditorDisplayableViewModel> CurrentDisplayEditorViewModels { get; } = new();

        protected override void OnViewLoaded(object v)
        {
            base.OnViewLoaded(v);
            RedrawUnitCloseXLines();
            InitExtraMenuItems();
        }

        private void RedrawTimeline()
        {
            using var __ = TGridUnitLineLocations.ToHashSetWithObjectPool(out var removeUnusedSet);
            var reuse = 0;
            var addnew = 0;
            void TryAddUnitLine(TGrid tGrid, double y, int i)
            {
                //Log.LogDebug($"add to {tGrid} {y:F2} {i}");
                if (removeUnusedSet.FirstOrDefault(x => x.Y == y) is TGridUnitLineViewModel lineModel)
                {
                    removeUnusedSet.Remove(lineModel);
                    lineModel.TGrid = tGrid;
                    lineModel.BeatRhythm = i;
                    reuse++;
                }
                else
                {
                    var newLineModel = ObjectPool<TGridUnitLineViewModel>.Get();
                    newLineModel.TGrid = tGrid;
                    newLineModel.BeatRhythm = i;
                    newLineModel.Y = y;

                    TGridUnitLineLocations.InsertBySortBy(newLineModel, x => x.Y);
                    addnew++;
                }
            }

            foreach ((TGrid tGrid, double y, int i) in TGridCalculator.GetVisbleTimelines(this, 240))
            {
                TryAddUnitLine(tGrid, TotalDurationHeight - y, i);
            }

            //删除不用的
            foreach (var unusedLine in removeUnusedSet)
            {
                ObjectPool<TGridUnitLineViewModel>.Return(unusedLine);
                TGridUnitLineLocations.Remove(unusedLine);
            }

            //Log.LogDebug($"AddCount:{reuse + addnew} , reuse ({reuse * 1.0 / (reuse + addnew) * 100:F2}%): {reuse} , addnew: {addnew} ,unused: {removeUnusedSet.Count}");
            removeUnusedSet.Clear();
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

            Fumen.GetAllDisplayableObjects(min, max)
                .Concat(CurrentSelectedObjects.OfType<OngekiObjectBase>().Where(x => Fumen.Contains(x))
                .OfType<IDisplayableObject>())
                .Distinct()
                .ForEach(x => allDisplayableObjects.Add(x));
            EditorViewModels.OfType<IEditorDisplayableViewModel>().ForEach(x => currentDisplayingObjects.Add(x.DisplayableObject));

            //检查当前显示的物件是否还在谱面中，不在就删除，在就更新位置
            foreach (var viewModel in EditorViewModels.OfType<IEditorDisplayableViewModel>())
            {
                var refObject = viewModel.DisplayableObject;
                //检查是否还存在
                if (!allDisplayableObjects.Contains(refObject))
                    removeObjects.Add(viewModel);
            }
            foreach (var removeViewModel in removeObjects)
            {
                EditorViewModels.Remove(removeViewModel);
                CurrentDisplayEditorViewModels.Remove(removeViewModel);
                if (removeViewModel.DisplayableObject is ISelectableObject selectable)
                    CurrentSelectedObjects.Remove(selectable);
            }

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
                if ((!currentDisplaying.DisplayableObject.CheckVisiable(min, max)) // 在显示范围外
                    && (currentDisplaying.DisplayableObject is not ISelectableObject selectable || !selectable.IsSelected)) // 并非选择状态
                {
                    //remove
                    removeObjects.Add(currentDisplaying);
                }
            }
            /*
            removeObjects.ForEach(x => CurrentDisplayEditorViewModels.Remove(x));
            using var d4 = CurrentDisplayEditorViewModels
                .GroupJoin(EditorViewModels.Where(x => x.DisplayableObject.CheckVisiable(min, max)), x => x, x => x, (x, _) => x)
                .ToListWithObjectPool(out var addList);
            foreach (var add in addList)
            {
                CurrentDisplayEditorViewModels.Add(add);
            }
            */
            using var d4 = EditorViewModels.Where(x => x.DisplayableObject.CheckVisiable(min, max)).ToListWithObjectPool(out var visibleList);
            using var d5 = visibleList.Except(CurrentDisplayEditorViewModels).ToListWithObjectPool(out var addList);
            CurrentDisplayEditorViewModels.AddRange(addList);

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
            var unitSize = XGridCalculator.CalculateXUnitSize(this);
            var totalUnitValue = 0d;
            var line = default(XGridUnitLineViewModel);

            for (double totalLength = width / 2 + unitSize; totalLength < width; totalLength += unitSize)
            {
                totalUnitValue += Setting.XGridUnitSpace;

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
            scrollViewer?.InvalidateMeasure();

            var view = GetView() as FrameworkElement;

            CanvasWidth = view.ActualWidth;
            CanvasHeight = view.ActualHeight;

            ClearDisplayingObjectCache();
            Redraw(RedrawTarget.All);
        }
    }
}
