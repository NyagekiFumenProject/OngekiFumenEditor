using Caliburn.Micro;
using OngekiFumenEditor.Base.OngekiObjects.Projectiles;
using OngekiFumenEditor.Base.OngekiObjects.Projectiles.Enums;
using OngekiFumenEditor.Modules.FumenVisualEditor.Base;
using OngekiFumenEditor.Properties;
using OngekiFumenEditor.UI.KeyBinding.Input;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels
{
    public partial class FumenVisualEditorViewModel
    {
        public void KeyboardAction_QuickBulletDamageUp(ActionExecutionContext context)
            => ChangeSelectedBulletDamage(context?.EventArgs as KeyEventArgs, true);

        public void KeyboardAction_QuickBulletDamageDown(ActionExecutionContext context)
            => ChangeSelectedBulletDamage(context?.EventArgs as KeyEventArgs, false);

        private void ChangeSelectedBulletDamage(KeyEventArgs e, bool forward)
        {
            if (e is null || e.Handled || !IsDesignMode || Fumen is null || EditorManager.CurrentActivatedEditor != this)
                return;

            if (TextInputFocusHelper.IsTextInput(Keyboard.FocusedElement as DependencyObject) ||
                TextInputFocusHelper.IsTextInput(e.OriginalSource as DependencyObject))
                return;

            List<(Bullet Bullet, BulletDamageType Before, BulletDamageType After)> changes = null;
            foreach (var selected in SelectObjects)
            {
                if (selected is not Bullet bullet)
                    continue;

                var before = bullet.BulletDamageTypeValue;
                var after = before switch
                {
                    BulletDamageType.Normal => forward ? BulletDamageType.Hard : BulletDamageType.Danger,
                    BulletDamageType.Hard => forward ? BulletDamageType.Danger : BulletDamageType.Normal,
                    BulletDamageType.Danger => forward ? BulletDamageType.Normal : BulletDamageType.Hard,
                    _ => before
                };
                if (before == after)
                    continue;

                changes ??= new();
                changes.Add((bullet, before, after));
            }

            if (changes is null)
                return;

            UndoRedoManager.ExecuteAction(LambdaUndoAction.Create(Resources.UndoChangeBulletDamage, () =>
            {
                foreach (var change in changes)
                    change.Bullet.BulletDamageTypeValue = change.After;
            }, () =>
            {
                foreach (var change in changes)
                    change.Bullet.BulletDamageTypeValue = change.Before;
            }));
            e.Handled = true;
        }
    }
}
