using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Base.OngekiObjects.Projectiles;
using OngekiFumenEditor.Modules.FumenBulletPalleteListViewer;
using OngekiFumenEditor.Modules.FumenVisualEditor.Base;
using OngekiFumenEditor.Modules.FumenVisualEditor.Models;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels.Interactives.Impls;
using OngekiFumenEditor.Properties;
using System;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Input;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels
{
    public partial class FumenVisualEditorViewModel
    {
        private IPlaceBatchProjectileInfo requestBatchPlaceProjectileInfo;
        private OngekiFumen batchPlacementFumen;
        private EditorProjectDataModel batchPlacementProject;
        private OngekiMovableObjectBase batchPlacementCandidate;
        private IPlaceBatchProjectileInfo batchPlacementGestureRequest;
        private Point batchPlacementPointer;
        private Point batchPlacementResolvedPoint;
        private float batchPlacementResolvedWidth;
        private float batchPlacementResolvedHeight;
        private bool batchPlacementPointerInside;
        private bool batchPlacementPositionDirty = true;
        private bool batchPlacementPositionValid;
        private bool batchPlacementPreviewVisible;
        private bool consumeBatchPlacementLeftMouseUp;
        private bool consumeBatchPlacementRightMouseUp;

        private void OnPlacementActiveEditorChanged(FumenVisualEditorViewModel newEditor, FumenVisualEditorViewModel oldEditor)
        {
            if (!ReferenceEquals(EditorManager.CurrentActivatedEditor, this))
                CancelBatchProjectilePlacement();
        }

        public IPlaceBatchProjectileInfo RequestBatchPlaceProjectileInfo => requestBatchPlaceProjectileInfo;
        public bool IsRequestPlaceBatchProjectilePreview => requestBatchPlaceProjectileInfo is not null;
        public bool CanRequestBatchProjectilePlacement => Volatile.Read(ref isDisposed) == 0 &&
            EditorProjectData?.Fumen is not null && IsDesignMode && !IsBatchMode &&
            ReferenceEquals(EditorManager.CurrentActivatedEditor, this);
        public OngekiMovableObjectBase BatchProjectilePlacementPreview =>
            batchPlacementPreviewVisible ? batchPlacementCandidate : null;

        public void ToggleBatchProjectilePlacement(BulletPallete pallete, bool isBell)
        {
            if (!CanRequestBatchProjectilePlacement || pallete is null || !Fumen.BulletPalleteList.Contains(pallete))
                return;

            if (requestBatchPlaceProjectileInfo is { } current &&
                ReferenceEquals(current.Pallete, pallete) && current.IsBell == isBell)
            {
                CancelBatchProjectilePlacement();
                return;
            }

            ClearBatchProjectilePlacement();
            requestBatchPlaceProjectileInfo = new PlaceBatchProjectileInfo(pallete, isBell);
            batchPlacementCandidate = CreateBatchProjectile(requestBatchPlaceProjectileInfo);
            batchPlacementFumen = Fumen;
            batchPlacementProject = EditorProjectData;
            batchPlacementFumen.BulletPalleteList.CollectionChanged += OnBatchPlacementPalettesChanged;
            batchPlacementFumen.ObjectModifiedChanged += OnBatchPlacementFumenModified;
            batchPlacementFumen.BpmList.OnChangedEvent += InvalidateBatchPlacementPosition;
            batchPlacementFumen.MeterChanges.OnChangedEvent += InvalidateBatchPlacementPosition;
            batchPlacementFumen.SoflansMap.OnCollectionChangedEvent += OnBatchPlacementSoflansChanged;
            batchPlacementProject.PropertyChanged += OnBatchPlacementProjectChanged;
            Setting.PropertyChanged += OnBatchPlacementSettingChanged;
            SelectionArea.IsActive = false;
            isSelectRangeDragging = false;
            mouseDownHitObject = null;
            mouseDownNextHitObject = null;
            NotifyBatchPlacementRequestChanged();
        }

        public void CancelBatchProjectilePlacement()
        {
            if (requestBatchPlaceProjectileInfo is null)
                return;

            ClearBatchProjectilePlacement();
            NotifyBatchPlacementRequestChanged();
        }

        private void ClearBatchProjectilePlacement()
        {
            if (batchPlacementFumen is { } fumen)
            {
                fumen.BulletPalleteList.CollectionChanged -= OnBatchPlacementPalettesChanged;
                fumen.ObjectModifiedChanged -= OnBatchPlacementFumenModified;
                fumen.BpmList.OnChangedEvent -= InvalidateBatchPlacementPosition;
                fumen.MeterChanges.OnChangedEvent -= InvalidateBatchPlacementPosition;
                fumen.SoflansMap.OnCollectionChangedEvent -= OnBatchPlacementSoflansChanged;
            }
            if (batchPlacementProject is not null)
                batchPlacementProject.PropertyChanged -= OnBatchPlacementProjectChanged;
            Setting.PropertyChanged -= OnBatchPlacementSettingChanged;

            if (batchPlacementCandidate is IBulletPalleteReferencable projectile)
                projectile.ReferenceBulletPallete = null;
            batchPlacementCandidate?.Dispose();
            batchPlacementCandidate = null;
            batchPlacementFumen = null;
            batchPlacementProject = null;
            requestBatchPlaceProjectileInfo = null;
            batchPlacementGestureRequest = null;
            batchPlacementPointerInside = false;
            batchPlacementPositionDirty = true;
            batchPlacementPositionValid = false;
            batchPlacementPreviewVisible = false;
            // Mouse-up suppression belongs to the gesture, not the cancelled session.
        }

        private void NotifyBatchPlacementRequestChanged()
        {
            NotifyOfPropertyChange(nameof(RequestBatchPlaceProjectileInfo));
            NotifyOfPropertyChange(nameof(IsRequestPlaceBatchProjectilePreview));
        }

        private void OnBatchPlacementPalettesChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (requestBatchPlaceProjectileInfo is { } request &&
                !batchPlacementFumen.BulletPalleteList.Contains(request.Pallete))
                CancelBatchProjectilePlacement();
        }

        private void OnBatchPlacementProjectChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(EditorProjectDataModel.Fumen))
            {
                CancelBatchProjectilePlacement();
                NotifyOfPropertyChange(nameof(Fumen));
                NotifyOfPropertyChange(nameof(CanRequestBatchProjectilePlacement));
            }
            else
                InvalidateBatchPlacementPosition();
        }

        private void OnBatchPlacementSettingChanged(object sender, PropertyChangedEventArgs e)
            => InvalidateBatchPlacementPosition();

        private void OnBatchPlacementFumenModified(OngekiObjectBase sender, PropertyChangedEventArgs e)
            => InvalidateBatchPlacementPosition();

        private void OnBatchPlacementSoflansChanged(CollectionChangeEventArgs e)
            => InvalidateBatchPlacementPosition();

        private void InvalidateBatchPlacementPosition() => batchPlacementPositionDirty = true;

        private static OngekiMovableObjectBase CreateBatchProjectile(IPlaceBatchProjectileInfo request)
            => request.IsBell
                ? new Bell { ReferenceBulletPallete = request.Pallete }
                : new Bullet { ReferenceBulletPallete = request.Pallete };

        private void UpdateBatchPlacementPointer(Point viewportPoint)
        {
            batchPlacementPointer = viewportPoint;
            batchPlacementPointerInside = double.IsFinite(viewportPoint.X) && double.IsFinite(viewportPoint.Y) &&
                viewportPoint.X >= 0 && viewportPoint.Y >= 0 &&
                viewportPoint.X < ViewWidth && viewportPoint.Y < ViewHeight;
            RefreshBatchProjectilePlacementPreview();
        }

        private void HideBatchPlacementPreview()
        {
            batchPlacementPointerInside = false;
            batchPlacementPreviewVisible = false;
        }

        public void RefreshBatchProjectilePlacementPreview()
        {
            batchPlacementPreviewVisible = false;
            if (requestBatchPlaceProjectileInfo is null || !batchPlacementPointerInside || !IsDesignMode ||
                Volatile.Read(ref isDisposed) != 0 || !ReferenceEquals(batchPlacementFumen, EditorProjectData?.Fumen))
                return;

            if (batchPlacementPointer.X >= ViewWidth || batchPlacementPointer.Y >= ViewHeight ||
                ViewWidth <= 0 || ViewHeight <= 0)
                return;

            var worldPoint = new Point(batchPlacementPointer.X, RectInDesignMode.MaxY - batchPlacementPointer.Y);
            if (!IsProjectilePositionAllowed(worldPoint))
                return;

            if (batchPlacementPositionDirty || worldPoint != batchPlacementResolvedPoint ||
                ViewWidth != batchPlacementResolvedWidth || ViewHeight != batchPlacementResolvedHeight)
            {
                batchPlacementPositionDirty = false;
                batchPlacementResolvedPoint = worldPoint;
                batchPlacementResolvedWidth = ViewWidth;
                batchPlacementResolvedHeight = ViewHeight;
                batchPlacementPositionValid = TryPositionProjectile(batchPlacementCandidate, worldPoint);
            }
            batchPlacementPreviewVisible = batchPlacementPositionValid;
        }

        private bool IsProjectilePositionAllowed(Point worldPoint)
            => double.IsFinite(worldPoint.X) && double.IsFinite(worldPoint.Y) &&
                (EditorGlobalSetting.Default.EnablePlaceObjectBeyondAudioDuration ||
                 worldPoint.Y >= 0 && worldPoint.Y <= TotalDurationHeight);

        private bool TryPositionProjectile(OngekiMovableObjectBase projectile, Point worldPoint)
            => ((DefaultObjectInteractiveAction)InteractiveManager.GetInteractive(projectile))
                .TryMoveCanvas(projectile, worldPoint, this);

        internal bool EndBatchPlacementGestureWithoutCommit()
        {
            var consume = consumeBatchPlacementLeftMouseUp;
            consumeBatchPlacementLeftMouseUp = false;
            batchPlacementGestureRequest = null;
            return consume;
        }

        internal bool BeginBatchPlacementRightGesture()
        {
            consumeBatchPlacementRightMouseUp = false;
            if (!IsRequestPlaceBatchProjectilePreview)
                return false;

            consumeBatchPlacementRightMouseUp = true;
            CancelBatchProjectilePlacement();
            return true;
        }

        internal bool ConsumeBatchPlacementRightMouseUp()
        {
            if (!consumeBatchPlacementRightMouseUp)
                return false;
            consumeBatchPlacementRightMouseUp = false;
            return true;
        }

        public bool TryHandleBatchProjectilePlacementMouseDown(MouseButton button, Point viewportPoint)
        {
            if (button == MouseButton.Right)
                return BeginBatchPlacementRightGesture();
            if (button != MouseButton.Left)
                return false;

            consumeBatchPlacementLeftMouseUp = false;
            batchPlacementGestureRequest = null;
            if (!IsRequestPlaceBatchProjectilePreview)
                return false;

            UpdateBatchPlacementPointer(viewportPoint);
            consumeBatchPlacementLeftMouseUp = true;
            if (batchPlacementPreviewVisible)
                batchPlacementGestureRequest = requestBatchPlaceProjectileInfo;
            return true;
        }

        public bool TryHandleBatchProjectilePlacementMouseUp(MouseButton button, Point viewportPoint)
        {
            if (button == MouseButton.Right)
                return ConsumeBatchPlacementRightMouseUp();
            if (button != MouseButton.Left || !consumeBatchPlacementLeftMouseUp)
                return false;

            consumeBatchPlacementLeftMouseUp = false;
            var gestureRequest = batchPlacementGestureRequest;
            batchPlacementGestureRequest = null;
            UpdateBatchPlacementPointer(viewportPoint);
            if (gestureRequest is not null && ReferenceEquals(gestureRequest, requestBatchPlaceProjectileInfo) &&
                batchPlacementPreviewVisible && CanRequestBatchProjectilePlacement &&
                batchPlacementFumen.BulletPalleteList.Contains(gestureRequest.Pallete))
            {
                var projectile = CreateBatchProjectile(gestureRequest);
                projectile.TGrid = batchPlacementCandidate.TGrid.CopyNew();
                projectile.XGrid = batchPlacementCandidate.XGrid.CopyNew();
                CommitProjectilePlacement(projectile);
            }
            return true;
        }

        internal bool PlaceProjectileAt(OngekiMovableObjectBase projectile, Point worldPoint)
        {
            if (projectile is not (Bullet or Bell))
                throw new ArgumentException("Only Bullet and Bell can use projectile placement.", nameof(projectile));

            var pallete = ((IBulletPalleteReferencable)projectile).ReferenceBulletPallete;
            if (!IsDesignMode || EditorProjectData?.Fumen is null ||
                pallete is not null && !Fumen.BulletPalleteList.Contains(pallete) ||
                !IsProjectilePositionAllowed(worldPoint) || !TryPositionProjectile(projectile, worldPoint))
            {
                ((IBulletPalleteReferencable)projectile).ReferenceBulletPallete = null;
                projectile.Dispose();
                return false;
            }

            CommitProjectilePlacement(projectile);
            return true;
        }

        private void CommitProjectilePlacement(OngekiMovableObjectBase projectile)
        {
            var fumen = Fumen;
            var tUnit = projectile.TGrid.Unit;
            var tGrid = projectile.TGrid.Grid;
            var xUnit = projectile.XGrid.Unit;
            var xGrid = projectile.XGrid.Grid;
            var pallete = ((IBulletPalleteReferencable)projectile).ReferenceBulletPallete;
            var damage = (projectile as Bullet)?.BulletDamageTypeValue;
            var first = true;
            UndoRedoManager.ExecuteAction(LambdaUndoAction.Create(Resources.UndoQuickProjectilePlacement, () =>
            {
                if (!first)
                {
                    projectile.TGrid = new TGrid(tUnit, tGrid);
                    projectile.XGrid = new XGrid(xUnit, xGrid);
                    ((IBulletPalleteReferencable)projectile).ReferenceBulletPallete = pallete;
                    if (projectile is Bullet bullet)
                        bullet.BulletDamageTypeValue = damage.Value;
                }
                fumen.AddObject(projectile);
                if (first)
                {
                    first = false;
                    NotifyObjectClicked(projectile);
                }
            }, () => fumen.RemoveObject(projectile)));
        }
    }
}
