using Caliburn.Micro;
using Gemini.Framework.Services;
using Gemini.Modules.Toolbox;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Modules.FumenObjectPropertyBrowser;
using OngekiFumenEditor.Modules.FumenSoflanGroupListViewer.Models;
using OngekiFumenEditor.Modules.FumenVisualEditor.Base;
using OngekiFumenEditor.Modules.FumenVisualEditor.Models;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels.Interactives.Impls;
using OngekiFumenEditor.Properties;
using OngekiFumenEditor.Utils;
using System;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Input;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels
{
    public partial class FumenVisualEditorViewModel
    {
        private readonly IsaPlacementSession isaPlacementSession = new();

        public bool IsIsaPlacementActive => isaPlacementSession.IsActive;
        public IsaPlacementPhase IsaPlacementPhase => isaPlacementSession.Phase;
        public int IsaPlacementTargetSoflanGroupId => isaPlacementSession.TargetSoflanGroupId;

        /// <summary>
        /// 当前渲染控件应使用的鼠标光标。ISA 两点放置期间为十字光标；视图通过数据绑定应用，
        /// 不再由后置代码订阅 PropertyChanged 手工赋值。
        /// </summary>
        public Cursor CurrentCursor => IsIsaPlacementActive ? Cursors.Cross : null;

        public string IsaPlacementTargetGroupDisplayName
        {
            get
            {
                var id = isaPlacementSession.TargetSoflanGroupId;
                var group = Fumen?.IndividualSoflanAreaMap.SoflanGroupTree.GetGroup(id);
                return group is null ? $"#{id}" : SoflanGroupDisplayName.FormatGroup(group);
            }
        }

        public string IsaPlacementStatusText => IsaPlacementPhase switch
        {
            IsaPlacementPhase.WaitingFirstPoint => Resources.IsaPlacementOverlayWaitingFirstPoint,
            IsaPlacementPhase.WaitingSecondPoint => Resources.IsaPlacementOverlayWaitingSecondPoint,
            _ => string.Empty,
        };

        public string IsaPlacementOverlaySummary
            => string.Format(Resources.IsaPlacementOverlaySummary, IsaPlacementTargetGroupDisplayName, IsaPlacementStatusText);

        public bool IsContinuousIsaPlacement
        {
            get => isaPlacementSession.Continuous;
            set
            {
                if (isaPlacementSession.Continuous == value)
                    return;
                isaPlacementSession.Continuous = value;
                NotifyOfPropertyChange();
            }
        }

        public bool CanStartIsaPlacement => Volatile.Read(ref isDisposed) == 0
            && Fumen is not null
            && IsDesignMode
            && !IsBatchMode
            && ReferenceEquals(EditorManager.CurrentActivatedEditor, this);

        private Window focusedWindow;

        private void HookWindowFocus()
        {
            var window = Application.Current?.MainWindow;
            if (window is null || ReferenceEquals(focusedWindow, window))
                return;
            UnhookWindowFocus();
            focusedWindow = window;
            focusedWindow.Deactivated += OnFocusedWindowDeactivated;
            // View 不处理输入事件；窗口级工具箱拖放由 ViewModel 监听并转为取消选择拖动。
            focusedWindow.AddHandler(DragDrop.PreviewDragEnterEvent, new DragEventHandler(OnToolboxDragStarted), true);
        }

        private void UnhookWindowFocus()
        {
            if (focusedWindow is null)
                return;
            focusedWindow.Deactivated -= OnFocusedWindowDeactivated;
            focusedWindow.RemoveHandler(DragDrop.PreviewDragEnterEvent, new DragEventHandler(OnToolboxDragStarted));
            focusedWindow = null;
        }

        private void OnFocusedWindowDeactivated(object sender, EventArgs e)
        {
            AbortSelectionDrag();
            if (ReferenceEquals(EditorManager.CurrentActivatedEditor, this))
                CancelIsaPlacement();
        }

        private void OnToolboxDragStarted(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(ToolboxDragDrop.DataFormat))
                AbortSelectionDrag();
        }

        public bool TryStartIsaPlacement(int soflanGroupId)
        {
            if (!CanStartIsaPlacement)
            {
                ToastNotify(Resources.IsaPlacementCannotStart);
                return false;
            }

            var tree = Fumen.IndividualSoflanAreaMap.SoflanGroupTree;
            if (!tree.ContainsGroup(soflanGroupId))
            {
                ToastNotify(Resources.IsaPlacementTargetGroupInvalid);
                return false;
            }

            CancelBatchProjectilePlacement();
            isaPlacementSession.Begin(soflanGroupId);
            NotifyIsaPlacementStateChanged();
            return true;
        }

        public void CancelIsaPlacement()
        {
            if (!isaPlacementSession.IsActive)
                return;

            isaPlacementSession.Cancel();
            NotifyIsaPlacementStateChanged();
        }

        public void CancelIsaPlacementIfTargetGroupRemoved(int soflanGroupId)
        {
            if (isaPlacementSession.IsActive && isaPlacementSession.TargetSoflanGroupId == soflanGroupId)
            {
                CancelIsaPlacement();
                ToastNotify(Resources.IsaPlacementTargetGroupInvalid);
            }
        }

        private void NotifyIsaPlacementStateChanged()
        {
            NotifyOfPropertyChange(nameof(IsIsaPlacementActive));
            NotifyOfPropertyChange(nameof(CurrentCursor));
            NotifyOfPropertyChange(nameof(IsaPlacementPhase));
            NotifyOfPropertyChange(nameof(IsaPlacementTargetSoflanGroupId));
            NotifyOfPropertyChange(nameof(IsaPlacementTargetGroupDisplayName));
            NotifyOfPropertyChange(nameof(IsaPlacementOverlaySummary));
            NotifyOfPropertyChange(nameof(IsaPlacementStatusText));
            NotifyOfPropertyChange(nameof(CanStartIsaPlacement));
        }

        public bool TryTakeIsaPlacementPoint(Point viewPosition)
        {
            if (!isaPlacementSession.IsActive)
                return false;

            if (!TryGetIsaPlacementPoint(viewPosition, out var tGrid, out var xGrid))
                return false;

            var targetGroupId = isaPlacementSession.TargetSoflanGroupId;
            if (isaPlacementSession.TryTakePoint(tGrid, xGrid, out var result))
                CompleteIsaPlacement(targetGroupId, result);
            else
                NotifyIsaPlacementStateChanged();
            return true;
        }

        /// <summary>
        /// 放置模式下拦截鼠标：左键取点，右键取消。返回 true 表示本次事件已被放置流程消费。
        /// </summary>
        private bool TryHandleIsaPlacementMouseDown(MouseButtonEventArgs arg, Point viewPosition)
        {
            if (!isaPlacementSession.IsActive)
                return false;

            if (arg.ChangedButton == MouseButton.Right)
            {
                CancelIsaPlacement();
                arg.Handled = true;
                return true;
            }

            if (arg.ChangedButton != MouseButton.Left)
                return false;

            // 越界等失败由具体检查提示，保留当前阶段让用户重新取点。
            TryTakeIsaPlacementPoint(viewPosition);
            arg.Handled = true;
            return true;
        }

        private bool TryGetIsaPlacementPoint(Point viewPosition, out TGrid tGrid, out XGrid xGrid)
        {
            tGrid = null;
            xGrid = null;

            var worldY = RectInDesignMode.MaxY - viewPosition.Y;
            if (worldY < 0)
            {
                ToastNotify(Resources.DisableAddObjectBeyondAudioDuration);
                return false;
            }

            var worldPoint = new Point(viewPosition.X, Math.Max(0, worldY));
            if (!CheckAndNotifyIfPlaceBeyondDuration(worldPoint))
                return false;

            var probe = new IndividualSoflanArea();
            if (InteractiveManager.GetInteractive(probe) is not DefaultObjectInteractiveAction interactive)
                return false;
            if (!interactive.TryMoveCanvas(probe, worldPoint, this))
                return false;

            tGrid = probe.TGrid;
            xGrid = probe.XGrid;
            return true;
        }

        private void CompleteIsaPlacement(int targetSoflanGroupId, IsaPlacementResult result)
        {
            NotifyIsaPlacementStateChanged();

            if (Fumen?.IndividualSoflanAreaMap.SoflanGroupTree.ContainsGroup(targetSoflanGroupId) != true)
            {
                CancelIsaPlacement();
                ToastNotify(Resources.IsaPlacementTargetGroupInvalid);
                return;
            }

            var isa = new IndividualSoflanArea
            {
                SoflanGroup = targetSoflanGroupId,
                TGrid = result.StartTGrid,
                XGrid = result.StartXGrid,
                EndIndicator =
                {
                    TGrid = result.EndTGrid,
                    XGrid = result.EndXGrid,
                },
            };

            var objBrowser = IoC.Get<IFumenObjectPropertyBrowser>();

            void SelectIsa()
            {
                foreach (var selected in SelectObjects.ToArray())
                    selected.IsSelected = false;
                isa.IsSelected = true;
                objBrowser.RefreshSelected(this);
                IoC.Get<IShell>().ActiveLayoutItem = this;
            }

            UndoRedoManager.ExecuteAction(LambdaUndoAction.Create(Resources.IsaPlacementAdd, () =>
            {
                Fumen.AddObject(isa);
                SelectIsa();
            }, () =>
            {
                isa.IsSelected = false;
                RemoveObject(isa);
            }));
        }
    }
}
