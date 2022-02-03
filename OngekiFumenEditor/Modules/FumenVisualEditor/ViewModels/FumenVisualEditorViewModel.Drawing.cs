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
            using var __ = TGridUnitLineLocations.ToHashSetWithObjectPool(out var removeUnusedSet);
            var reuse = 0;
            var addnew = 0;
            void TryAddUnitLine(TGrid tGrid, double y, int i)
            {
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

            var bpmList = Fumen.BpmList;
            var meterList = Fumen.MeterChanges;

            //划线的中止位置
            var endTGrid = TGridCalculator.ConvertYToTGrid(MaxVisibleCanvasY, this);
            //可显示划线的起始位置
            var currentTGridBase = TGridCalculator.ConvertYToTGrid(MinVisibleCanvasY, this) ?? TGridCalculator.ConvertYToTGrid(MinVisibleCanvasY + Setting.JudgeLineOffsetY, this);

            var timeSignatures = meterList.GetCachedAllTimeSignatureUniformPositionList(240, bpmList);
            var currentTimeSignatureIndex = 0;
            //快速定位,尽量避免计算完全不用画的timesignature(
            for (int i = 0; i < timeSignatures.Count - 1; i++)
            {
                var cur = timeSignatures[i];
                var next = timeSignatures[i + 1];

                if (next.startY > MinVisibleCanvasY)
                {
                    currentTimeSignatureIndex = i;
                    break;
                }
            }
            //钦定好要画的起始timeSignatrue
            (double startY, MeterChange meter, BPMChange bpm) currentTimeSignature = timeSignatures[currentTimeSignatureIndex];

            while (currentTGridBase is not null)
            {
                var nextTimeSignatureIndex = currentTimeSignatureIndex + 1;
                var nextTimeSignature = timeSignatures.Count > nextTimeSignatureIndex ? timeSignatures[nextTimeSignatureIndex] : default;

                //钦定好要画的相对于当前timeSignature的偏移Y，节拍信息，节奏速度
                (var currentStartY, var currentMeter, var currentBpm) = currentTimeSignature;
                (var nextStartY, _, var nextBpm) = nextTimeSignature;

                //计算每一拍的(grid)长度
                var lengthPerBeat = (int)(currentBpm.TGrid.ResT / (currentMeter.BunShi * Setting.BeatSplit));

                //这里也可以跳过添加完全看不到的线
                var diff = currentTGridBase - currentBpm.TGrid;
                var totalGrid = diff.Unit * currentBpm.TGrid.ResT + diff.Grid;
                var i = (int)Math.Max(0, totalGrid / lengthPerBeat);

                while (true)
                {
                    var tGrid = currentBpm.TGrid + new GridOffset(0, lengthPerBeat * i);
                    //因为是不存在跨bpm长度计算，可以直接CalculateBPMLength(...)计算而不是TGridCalculator.ConvertTGridToY(...);
                    var y = currentStartY + MathUtils.CalculateBPMLength(currentBpm, tGrid, 240);
                    //超过当前timeSignature范围，切换到下一个timeSignature画新的线
                    if (nextBpm is not null && y >= nextStartY)
                        break;
                    //超过编辑器谱面范围，后面都不用画了
                    if (tGrid > endTGrid)
                        goto endAdd;
                    //
                    TryAddUnitLine(tGrid, TotalDurationHeight - y, i);
                    i++;
                }
                currentTGridBase = nextBpm.TGrid;
                currentTimeSignatureIndex = nextTimeSignatureIndex;
                currentTimeSignature = timeSignatures[currentTimeSignatureIndex];
            }


        endAdd:

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
