using OngekiFumenEditor.Properties;
using Caliburn.Micro;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Modules.FumenObjectPropertyBrowser;
using OngekiFumenEditor.Modules.FumenSoflanGroupListViewer;
using OngekiFumenEditor.Modules.FumenVisualEditor.Base;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels.Interactives.Impls;
using System.Windows;
using System.Linq;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels
{
    public partial class FumenVisualEditorViewModel
    {
        private readonly TwoPointPlacement<(XGrid X, TGrid T)> individualSoflanPlacement = new();
        private int individualSoflanPlacementGroup;
        private bool consumeIndividualSoflanMouseUp;
        public bool IsIndividualSoflanPlacementActive => individualSoflanPlacement.IsActive;
        public string IndividualSoflanPlacementText => !individualSoflanPlacement.IsActive
            ? Resources.IsaPlacement : individualSoflanPlacement.HasFirstPoint
            ? Resources.IsaPlacementSecondPoint : Resources.IsaPlacementFirstPoint;

        private void NotifyIndividualSoflanPlacement()
        {
            NotifyOfPropertyChange(() => IsIndividualSoflanPlacementActive);
            NotifyOfPropertyChange(() => IndividualSoflanPlacementText);
        }

        public void CancelIndividualSoflanPlacement()
        {
            individualSoflanPlacement.Cancel();
            NotifyIndividualSoflanPlacement();
        }

        public void ToggleIndividualSoflanPlacement()
        {
            if (individualSoflanPlacement.IsActive)
            {
                CancelIndividualSoflanPlacement();
                return;
            }

            if (!IsDesignMode || IsBatchMode)
            {
                ToastNotify(Resources.IsaPlacementDesignModeRequired);
                return;
            }

            var selectedGroup = IoC.Get<IFumenSoflanGroupListViewer>().CurrentSelectedSoflanGroupWrapItem;
            if (selectedGroup is null || !Fumen.IndividualSoflanAreaMap.GetSoflanGroups().Any(x => x.SoflanGroupId == selectedGroup.SoflanGroupId))
            {
                ToastNotify(Resources.IsaPlacementGroupRequired);
                return;
            }

            individualSoflanPlacementGroup = selectedGroup.SoflanGroupId;
            individualSoflanPlacement.Toggle();
            SelectionArea.IsActive = false;
            mouseDownHitObject = null;
            mouseDownNextHitObject = null;
            NotifyIndividualSoflanPlacement();
        }

        private bool TryPlaceIndividualSoflanPoint(Point position)
        {
            if (!individualSoflanPlacement.IsActive)
                return false;
            consumeIndividualSoflanMouseUp = true;
            if (!CheckAndNotifyIfPlaceBeyondDuration(position))
                return true;
            if (!Fumen.IndividualSoflanAreaMap.GetSoflanGroups().Any(x => x.SoflanGroupId == individualSoflanPlacementGroup))
            {
                CancelIndividualSoflanPlacement();
                ToastNotify(Resources.IsaPlacementGroupMissing);
                return true;
            }

            // 沿用普通物件的时间及 X 吸附规则；两个角点分别吸附。
            var probe = new IndividualSoflanArea();
            var interaction = new DefaultObjectInteractiveAction();
            var x = interaction.CheckAndAdjustX(probe, position.X, this);
            var y = interaction.CheckAndAdjustY(probe, position.Y, this);
            var tGrid = y is double snappedY ? ConvertYToTGrid_DesignMode(snappedY) : null;
            if (x is not double snappedX || tGrid is null)
                return true;
            var point = (X: XGridCalculator.ConvertXToXGrid(snappedX, this), T: tGrid);
            if (individualSoflanPlacement.TryComplete(point, out var first))
            {
                // 规范化时间和水平边界，避免结束点早于起点被模型截断。
                var area = new IndividualSoflanArea
                {
                    SoflanGroup = individualSoflanPlacementGroup,
                    TGrid = (first.T <= point.T ? first.T : point.T).CopyNew(),
                    XGrid = (first.X <= point.X ? first.X : point.X).CopyNew()
                };
                area.EndIndicator.TGrid = (first.T <= point.T ? point.T : first.T).CopyNew();
                area.EndIndicator.XGrid = (first.X <= point.X ? point.X : first.X).CopyNew();
                UndoRedoManager.ExecuteAction(LambdaUndoAction.Create(Resources.IsaPlacementUndo,
                    () =>
                    {
                        Fumen.AddObject(area);
                        ClearSelection();
                        area.IsSelected = true;
                        IoC.Get<IFumenObjectPropertyBrowser>().RefreshSelected(this);
                    },
                    () =>
                    {
                        Fumen.RemoveObject(area);
                        IoC.Get<IFumenObjectPropertyBrowser>().RefreshSelected(this);
                    }));
            }

            NotifyIndividualSoflanPlacement();
            return true;
        }
    }
}
