using Caliburn.Micro;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Base.OngekiObjects.Projectiles;
using OngekiFumenEditor.Base.OngekiObjects.Projectiles.Enums;
using OngekiFumenEditor.Kernel.KeyBinding;
using OngekiFumenEditor.Modules.FumenVisualEditor.Base;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels.Interactives.Impls;
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
        public event System.Action QuickPlacementCancelled;
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
            if (quickPlacementPreviewObject is IBulletPalleteReferencable projectile) projectile.ReferenceBulletPallete = null;
            quickPlacementPreviewObject = null;
            quickPlacingDrag = false;
            NotifyOfPropertyChange(() => QuickPlacementVisibility);
            QuickPlacementCancelled?.Invoke();
        }

        public void BeginPaletteQuickPlacement(BulletPallete palette, bool bell)
        {
            if (!IsDesignMode || IsBatchMode || Fumen == null || !Fumen.BulletPalleteList.Contains(palette)) return;
            BeginQuickPlacement(bell ? () => new Bell { ReferenceBulletPallete = palette } : () => new Bullet { ReferenceBulletPallete = palette }, bell ? "◇" : "●");
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
            NotifyOfPropertyChange(() => QuickPlacementGlyph);
            UpdateQuickPlacementPreview();
        }

        private bool TryGetQuickPlacementGrid(Point position, out XGrid x, out TGrid t)
        {
            var probe = new Bell();
            var interaction = new DefaultObjectInteractiveAction();
            var sx = interaction.CheckAndAdjustX(probe, position.X, this);
            var sy = interaction.CheckAndAdjustY(probe, position.Y, this);
            x = sx is double px ? XGridCalculator.ConvertXToXGrid(px, this) : null;
            t = sy is double py ? ConvertYToTGrid_DesignMode(py) : null;
            return x != null && t != null;
        }

        private void UpdateQuickPlacementPreview()
        {
            RefreshPlacementCursor();
            if (IsQuickPlacementActive && CurrentCursorPosition is Point pos && TryGetQuickPlacementGrid(pos, out var x, out var t))
            {
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
                var shape = bullet.TypeValue switch { BulletType.Needle => "Needle", BulletType.Square => "Square", _ => "Circle" };
                // 新放置子弹的默认伤害为普通，复用正式绘制器的对应颜色资源。
                resource = "bullet" + shape + "Normal.png";
                if (bullet.TypeValue != BulletType.Circle) { width = 30; height = 80; }
                if (bullet.SizeValue == BulletSize.Large) { width *= 1.4; height *= 1.4; }
            }
            else if (obj is Bell bell)
            {
                resource = "bell.png";
                if (bell.SizeValue == BulletSize.Large) { width *= 1.4; height *= 1.4; }
            }
            else { width = 28; height = 36; }
            var image = resource == null ? null : System.IO.Path.Combine(AppContext.BaseDirectory, "Resources", "editor", resource);
            if (QuickPlacementImage != image)
            {
                QuickPlacementImage = image;
                NotifyOfPropertyChange(() => QuickPlacementImage);
                NotifyOfPropertyChange(() => QuickPlacementImageVisibility);
                NotifyOfPropertyChange(() => QuickPlacementGlyphVisibility);
            }
            if (QuickPlacementImageWidth != width) { QuickPlacementImageWidth = width; NotifyOfPropertyChange(() => QuickPlacementImageWidth); }
            if (QuickPlacementImageHeight != height) { QuickPlacementImageHeight = height; NotifyOfPropertyChange(() => QuickPlacementImageHeight); }
        }

        private void PlaceQuickObject(Point position)
        {
            if (!ValidateQuickPlacementPalette() || !IsDesignMode || IsBatchMode ||
                !CheckAndNotifyIfPlaceBeyondDuration(position) || !TryGetQuickPlacementGrid(position, out var x, out var t)) return;
            var obj = quickPlacementFactory();
            obj.XGrid = x.CopyNew();
            obj.TGrid = t.CopyNew();
            AddQuickObjectUndo(obj);
        }

        private void AddQuickObjectUndo(OngekiMovableObjectBase obj)
            => UndoRedoManager.ExecuteAction(LambdaUndoAction.Create(OngekiFumenEditor.Properties.Resources.UndoQuickProjectilePlacement, () => Fumen.AddObject(obj), () => Fumen.RemoveObject(obj)));

        internal static bool IsQuickPlacementTextInput(object target)
            => target is System.Windows.Controls.Primitives.TextBoxBase
                or System.Windows.Controls.PasswordBox or System.Windows.Controls.ComboBox;

        // 使用配置中的定义匹配，Preview 阶段消费事件以阻止旧绑定重复执行。
        public bool HandleQuickPlacementKey(KeyEventArgs e)
        {
            RefreshPlacementCursor();
            if (IsQuickPlacementTextInput(Keyboard.FocusedElement) || !IsDesignMode || IsBatchMode) return false;
            var manager = IoC.Get<IKeyBindingManager>();
            bool Match(KeyBindingDefinition definition) => manager.CheckKeyBinding(definition, e);
            if (Match(KeyBindingDefinitions.KBD_CancelSelectingObjects) && IsQuickPlacementActive)
            {
                CancelQuickPlacement(); return true;
            }
            if (Match(KeyBindingDefinitions.KBD_QuickBulletDamageUp) || Match(KeyBindingDefinitions.KBD_QuickBulletDamageDown))
            {
                if (!SelectObjects.OfType<Bullet>().Any()) return false;
                if (!e.IsRepeat) ChangeSelectedBulletDamage(Match(KeyBindingDefinitions.KBD_QuickBulletDamageUp) ? 1 : -1);
                return true;
            }
            return false;
        }

        private void ChangeSelectedBulletDamage(int direction)
        {
            var values = SelectObjects.OfType<Bullet>().Select(b => (Bullet: b, Old: b.BulletDamageTypeValue)).ToArray();
            if (values.Length == 0) return;
            UndoRedoManager.ExecuteAction(LambdaUndoAction.Create(OngekiFumenEditor.Properties.Resources.UndoChangeBulletDamage, () =>
            {
                foreach (var pair in values) pair.Bullet.BulletDamageTypeValue = (BulletDamageType)(((int)pair.Old + direction + 3) % 3);
            }, () => { foreach (var pair in values) pair.Bullet.BulletDamageTypeValue = pair.Old; }));
        }
    }
}
