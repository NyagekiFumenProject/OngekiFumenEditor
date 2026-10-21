using Caliburn.Micro;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Base.OngekiObjects.Projectiles;
using OngekiFumenEditor.Base.OngekiObjects.Projectiles.Enums;
using OngekiFumenEditor.Modules.FumenVisualEditor.Base;
using OngekiFumenEditor.Modules.FumenVisualEditor.Base.DropActions;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Input;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels
{
    public partial class FumenVisualEditorViewModel
    {
        private Func<OngekiMovableObjectBase> quickPlacementFactory;
        private OngekiMovableObjectBase quickPlacementPreviewObject;
        private OngekiFumen quickPlacementFumen;
        private bool quickPlacingDrag;
        private bool consumeQuickMouseUp;
        private bool consumeQuickRightMouseUp;
        private Point? placementViewportPoint;
        public BulletPallete QuickPlacementPalette => (quickPlacementPreviewObject as IBulletPalleteReferencable)?.ReferenceBulletPallete;
        public bool QuickPlacementIsBell => quickPlacementPreviewObject is Bell;
        public bool IsQuickPlacementActive => quickPlacementFactory != null;
        public string QuickPlacementGlyph { get; private set; }
        public string QuickPlacementImage { get; private set; }
        public double QuickPlacementImageWidth { get; private set; } = 40;
        public double QuickPlacementImageHeight { get; private set; } = 40;
        public Visibility QuickPlacementImageVisibility => QuickPlacementImage != null ? Visibility.Visible : Visibility.Collapsed;
        public Visibility QuickPlacementGlyphVisibility => QuickPlacementImage == null ? Visibility.Visible : Visibility.Collapsed;
        public double QuickPlacementX { get; private set; }
        public double QuickPlacementY { get; private set; }
        public Visibility QuickPlacementVisibility => IsQuickPlacementActive && CurrentCursorPosition.HasValue ? Visibility.Visible : Visibility.Collapsed;

        public void CancelQuickPlacement()
        {
            if (quickPlacementFumen != null)
                quickPlacementFumen.BulletPalleteList.CollectionChanged -= OnQuickPlacementPalettesChanged;
            quickPlacementFumen = null;
            quickPlacementFactory = null;
            if (quickPlacementPreviewObject is IBulletPalleteReferencable projectile)
                projectile.ReferenceBulletPallete = null;
            quickPlacementPreviewObject = null;
            quickPlacingDrag = false;
            NotifyOfPropertyChange(() => QuickPlacementVisibility);
            NotifyOfPropertyChange(() => QuickPlacementPalette);
            NotifyOfPropertyChange(() => QuickPlacementIsBell);
        }

        public void BeginPaletteQuickPlacement(BulletPallete palette, bool bell)
        {
            if (!IsDesignMode || IsBatchMode || Fumen == null || !Fumen.BulletPalleteList.Contains(palette))
                return;
            BeginQuickPlacement(bell ? () => new Bell
            {
                ReferenceBulletPallete = palette
            } : () => new Bullet
            {
                ReferenceBulletPallete = palette
            }, bell ? "◇" : "●");
            quickPlacementFumen = Fumen;
            quickPlacementFumen.BulletPalleteList.CollectionChanged += OnQuickPlacementPalettesChanged;
        }

        private void OnQuickPlacementPalettesChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
            => ValidateQuickPlacementPalette();

        private bool ValidateQuickPlacementPalette()
        {
            if (!IsQuickPlacementActive)
                return false;
            if (quickPlacementPreviewObject is not IBulletPalleteReferencable projectile ||
                projectile.ReferenceBulletPallete == null || Fumen == null ||
                (quickPlacementFumen != null && !ReferenceEquals(quickPlacementFumen, Fumen)) ||
                !Fumen.BulletPalleteList.Contains(projectile.ReferenceBulletPallete))
            {
                CancelQuickPlacement();
                return false;
            }

            return true;
        }

        private void BeginQuickPlacement(Func<OngekiMovableObjectBase> factory, string glyph)
        {
            CancelQuickPlacement();
            quickPlacementFactory = factory;
            quickPlacementPreviewObject = factory();
            QuickPlacementGlyph = glyph;
            SelectionArea.IsActive = false;
            mouseDownHitObject = null;
            mouseDownNextHitObject = null;
            NotifyOfPropertyChange(() => QuickPlacementPalette);
            NotifyOfPropertyChange(() => QuickPlacementIsBell);
            NotifyOfPropertyChange(() => QuickPlacementGlyph);
            UpdateQuickPlacementPreview();
        }

        private void UpdateQuickPlacementPreview()
        {
            RefreshPlacementCursor();
            if (IsQuickPlacementActive && CurrentCursorPosition is Point pos)
            {
                MoveObjectTo(quickPlacementPreviewObject, pos);
                var x = quickPlacementPreviewObject.XGrid;
                var t = quickPlacementPreviewObject.TGrid;
                UpdateQuickPlacementAppearance();
                QuickPlacementX = XGridCalculator.ConvertXGridToX(x, this) - QuickPlacementImageWidth / 2;
                QuickPlacementY = RectInDesignMode.MaxY - ConvertTGridToY_DesignMode(t) - QuickPlacementImageHeight / 2;
                NotifyOfPropertyChange(() => QuickPlacementX);
                NotifyOfPropertyChange(() => QuickPlacementY);
            }

            NotifyOfPropertyChange(() => QuickPlacementVisibility);
        }

        private void RefreshPlacementCursor()
        {
            // 保存视口坐标，滚动或缩放后重新换算，避免幽灵停在旧的谱面时间。
            if (CurrentCursorPosition.HasValue && placementViewportPoint is Point pointer)
                CurrentCursorPosition = new Point(pointer.X, RectInDesignMode.MaxY - pointer.Y);
        }

        private void UpdateQuickPlacementAppearance()
        {
            var obj = quickPlacementPreviewObject;
            string resource = null;
            var width = 40d;
            var height = 40d;
            if (obj is Bullet bullet)
            {
                var shape = bullet.TypeValue switch
                {
                    BulletType.Needle => "Needle",
                    BulletType.Square => "Square",
                    _ => "Circle"
                };
                resource = "bullet" + shape + bullet.BulletDamageTypeValue + ".png";
                if (bullet.TypeValue != BulletType.Circle)
                {
                    width = 30;
                    height = 80;
                }

                if (bullet.SizeValue == BulletSize.Large)
                {
                    width *= 1.4;
                    height *= 1.4;
                }
            }
            else if (obj is Bell bell)
            {
                resource = "bell.png";
                if (bell.SizeValue == BulletSize.Large)
                {
                    width *= 1.4;
                    height *= 1.4;
                }
            }
            else
            {
                width = 28;
                height = 36;
            }

            var image = resource == null ? null : System.IO.Path.Combine(AppContext.BaseDirectory, "Resources", "editor", resource);
            if (QuickPlacementImage != image)
            {
                QuickPlacementImage = image;
                NotifyOfPropertyChange(() => QuickPlacementImage);
                NotifyOfPropertyChange(() => QuickPlacementImageVisibility);
                NotifyOfPropertyChange(() => QuickPlacementGlyphVisibility);
            }

            if (QuickPlacementImageWidth != width)
            {
                QuickPlacementImageWidth = width;
                NotifyOfPropertyChange(() => QuickPlacementImageWidth);
            }

            if (QuickPlacementImageHeight != height)
            {
                QuickPlacementImageHeight = height;
                NotifyOfPropertyChange(() => QuickPlacementImageHeight);
            }
        }

        private void PlaceQuickObject(Point position)
        {
            if (!ValidateQuickPlacementPalette() || !IsDesignMode || IsBatchMode)
                return;
            new OngekiObjectDropParam(() =>
            {
                var obj = quickPlacementFactory();
                if (obj is Bullet bullet && quickPlacementPreviewObject is Bullet preview)
                    bullet.BulletDamageTypeValue = preview.BulletDamageTypeValue;
                return obj;
            }).Drop(this, position);
        }

        internal static bool IsQuickPlacementTextInput(object target)
            => target is System.Windows.Controls.Primitives.TextBoxBase
                or System.Windows.Controls.PasswordBox or System.Windows.Controls.ComboBox;
        public void KeyboardAction_QuickBulletDamageUp(ActionExecutionContext context)
            => ChangeQuickBulletDamage(context, 1);

        public void KeyboardAction_QuickBulletDamageDown(ActionExecutionContext context)
            => ChangeQuickBulletDamage(context, -1);

        private void ChangeQuickBulletDamage(ActionExecutionContext context, int direction)
        {
            if (IsQuickPlacementTextInput(Keyboard.FocusedElement) ||
                IsQuickPlacementTextInput((context.EventArgs as RoutedEventArgs)?.OriginalSource) || !IsDesignMode || IsBatchMode)
                return;
            if (context.EventArgs is KeyEventArgs key && key.IsRepeat)
                return;
            if (quickPlacementPreviewObject is Bullet preview)
            {
                preview.BulletDamageTypeValue = (BulletDamageType)(((int)preview.BulletDamageTypeValue + direction + 3) % 3);
                UpdateQuickPlacementPreview();
                if (context.EventArgs is KeyEventArgs handledKey)
                    handledKey.Handled = true;
            }
            else
                ChangeSelectedBulletDamage(direction);
        }

        private void ChangeSelectedBulletDamage(int direction)
        {
            var values = SelectObjects.OfType<Bullet>().Select(b => (Bullet: b, Old: b.BulletDamageTypeValue)).ToArray();
            if (values.Length == 0)
                return;
            UndoRedoManager.ExecuteAction(LambdaUndoAction.Create(OngekiFumenEditor.Properties.Resources.UndoChangeBulletDamage, () =>
            {
                foreach (var pair in values)
                    pair.Bullet.BulletDamageTypeValue = (BulletDamageType)(((int)pair.Old + direction + 3) % 3);
            }, () =>
            {
                foreach (var pair in values)
                    pair.Bullet.BulletDamageTypeValue = pair.Old;
            }));
        }
    }
}
