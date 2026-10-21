using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Caliburn.Micro;
using Gemini.Framework.Services;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Base.OngekiObjects.Projectiles;
using OngekiFumenEditor.Base.OngekiObjects.Projectiles.Enums;
using OngekiFumenEditor.Kernel.KeyBinding;
using OngekiFumenEditor.Kernel.SettingPages.KeyBinding.ViewModels;
using OngekiFumenEditor.Modules.FumenVisualEditor;
using OngekiFumenEditor.Modules.FumenVisualEditor.Commands.BatchModeToggle;
using OngekiFumenEditor.Modules.FumenVisualEditor.Kernel;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using OngekiFumenEditor.Modules.FumenVisualEditor.Views;

internal static class RoutedInputChecks
{
    private static void SetBinding(KeyBindingDefinition definition, Key key, ModifierKeys modifiers)
    {
        definition.Key = key;
        definition.Modifiers = modifiers;
    }

    public static void Run(Action<bool, string> check)
    {
        using var keyboardState = new ThreadKeyboardState();
        using var editor = CheckSupport.CreateEditor();
        var bullets = new[] { BulletDamageType.Normal, BulletDamageType.Hard, BulletDamageType.Danger }
            .Select(x => new Bullet { BulletDamageTypeValue = x, IsSelected = true }).ToArray();
        var bell = new Bell { IsSelected = true };
        editor.Fumen.AddObjects(bullets);
        editor.Fumen.AddObject(bell);
        var pallete = new BulletPallete();
        editor.Fumen.AddObject(pallete);
        editor.ToggleBatchProjectilePlacement(pallete, true);
        var request = editor.RequestBatchPlaceProjectileInfo;
        var before = bullets.Select(x => x.BulletDamageTypeValue).ToArray();
        var bellPosition = BehaviorChecks.Position(bell);
        var history = editor.UndoRedoManager.UndoActionCount;
        var source = new DetachedPresentationSource();
        KeyEventArgs Event(Key key, object? originalSource = null) => new(Keyboard.PrimaryDevice, source, Environment.TickCount, key)
        {
            RoutedEvent = Keyboard.KeyDownEvent,
            Source = originalSource
        };
        var up = Event(Key.Up);
        editor.KeyboardAction_QuickBulletDamageUp(new ActionExecutionContext { EventArgs = up });
        var forward = new[] { BulletDamageType.Hard, BulletDamageType.Danger, BulletDamageType.Normal };
        check(up.Handled && bullets.Select(x => x.BulletDamageTypeValue).SequenceEqual(forward), "damage up cycles each of Normal/Hard/Danger independently");
        check(bell.IsSelected && BehaviorChecks.Position(bell) == bellPosition && bullets.All(x => x.IsSelected), "mixed Bell and Bullet selection remains unchanged");
        check(ReferenceEquals(request, editor.RequestBatchPlaceProjectileInfo), "damage shortcut preserves placement template/type request");
        check(editor.UndoRedoManager.UndoActionCount == history + 1, "three damage changes form one undo action");
        editor.UndoRedoManager.Undo(1);
        check(bullets.Select(x => x.BulletDamageTypeValue).SequenceEqual(before), "damage batch undo restores each original damage");
        foreach (var bullet in bullets)
        {
            bullet.BulletDamageTypeValue = BulletDamageType.Danger;
            bullet.IsSelected = false;
        }
        editor.UndoRedoManager.Redo(1);
        check(bullets.Select(x => x.BulletDamageTypeValue).SequenceEqual(forward), "damage redo uses fixed targets despite changed selection and intervening values");
        foreach (var bullet in bullets) bullet.IsSelected = true;
        var down = Event(Key.Down);
        editor.KeyboardAction_QuickBulletDamageDown(new ActionExecutionContext { EventArgs = down });
        check(down.Handled && bullets.Select(x => x.BulletDamageTypeValue).SequenceEqual(before), "damage down reverses all three damage types independently");
        down = Event(Key.Down);
        editor.KeyboardAction_QuickBulletDamageDown(new ActionExecutionContext { EventArgs = down });
        check(bullets.Select(x => x.BulletDamageTypeValue).SequenceEqual(new[] { BulletDamageType.Danger, BulletDamageType.Normal, BulletDamageType.Hard }),
            "damage down wraps Normal to Danger");
        var handled = Event(Key.Up);
        handled.Handled = true;
        history = editor.UndoRedoManager.UndoActionCount;
        editor.KeyboardAction_QuickBulletDamageUp(new ActionExecutionContext { EventArgs = handled });
        check(editor.UndoRedoManager.UndoActionCount == history, "already-handled key does not execute again");

        // Bind the actual production XAML. RaiseEvent drives its real KeyDown triggers; no direct call is used below.
        var view = new FumenVisualEditorView();
        ViewModelBinder.Bind(editor, view, null);
        var root = (Grid)view.Content;
        var inputHost = new StackPanel();
        root.Children.Add(inputHost);
        var textInputs = new Control[] { new TextBox(), new RichTextBox(), new PasswordBox(), new ComboBox { IsEditable = true } };
        foreach (var input in textInputs) inputHost.Children.Add(input);
        var keySettings = new KeyBindingSettingViewModel();
        var originalKeys = keySettings.Definitions.Select(x => (Definition: x, x.Key, x.Modifiers)).ToArray();
        try
        {
            // Isolate unrelated user-configured shortcuts without changing any config files.
            foreach (var item in originalKeys) item.Definition.Key = Key.None;
            SetBinding(KeyBindingDefinitions.KBD_QuickBulletDamageUp, Key.Up, ModifierKeys.None);
            SetBinding(KeyBindingDefinitions.KBD_QuickBulletDamageDown, Key.Down, ModifierKeys.None);
            CheckSupport.Pump();
            var current = bullets.Select(x => x.BulletDamageTypeValue).ToArray();
            var routed = Event(Key.Up, view);
            view.RaiseEvent(routed);
            check(routed.Handled && bullets.Select(x => x.BulletDamageTypeValue).SequenceEqual(before), "actual FumenVisualEditorView Up binding advances each damage type");
            SetBinding(KeyBindingDefinitions.KBD_QuickBulletDamageUp, Key.F13, ModifierKeys.None);
            SetBinding(KeyBindingDefinitions.KBD_QuickBulletDamageDown, Key.F14, ModifierKeys.None);
            CheckSupport.Pump();
            current = bullets.Select(x => x.BulletDamageTypeValue).ToArray();
            var obsolete = Event(Key.Up, view);
            view.RaiseEvent(obsolete);
            check(!obsolete.Handled && bullets.Select(x => x.BulletDamageTypeValue).SequenceEqual(current), "old key stops matching after key-definition remap");
            routed = Event(Key.F13, view);
            view.RaiseEvent(routed);
            check(routed.Handled && bullets.Select(x => x.BulletDamageTypeValue).SequenceEqual(forward), "new unmodified key advances each damage type through actual view binding");
            current = bullets.Select(x => x.BulletDamageTypeValue).ToArray();
            routed = Event(Key.F14, view);
            view.RaiseEvent(routed);
            check(routed.Handled && bullets.Select(x => x.BulletDamageTypeValue).SequenceEqual(before), "new reverse key restores each damage type through actual view binding");

            SetBinding(KeyBindingDefinitions.KBD_QuickBulletDamageUp, Key.F13, ModifierKeys.Alt);
            keyboardState.SetModifiers(ModifierKeys.Alt);
            routed = Event(Key.F13, view);
            view.RaiseEvent(routed);
            check(routed.Handled && bullets.Select(x => x.BulletDamageTypeValue).SequenceEqual(forward),
                "Alt-remapped key executes through the real modifier matcher and damage route");
            foreach (var bullet in bullets) bullet.IsSelected = false;
            history = editor.UndoRedoManager.UndoActionCount;
            routed = Event(Key.F13, view);
            view.RaiseEvent(routed);
            check(!routed.Handled && editor.UndoRedoManager.UndoActionCount == history,
                "Alt-remapped key is not consumed when only Bell is selected");
            foreach (var bullet in bullets) bullet.IsSelected = true;
            foreach (var input in textInputs)
            {
                var altInput = Event(Key.F13, input);
                input.RaiseEvent(altInput);
                check(!altInput.Handled && bullets.Select(x => x.BulletDamageTypeValue).SequenceEqual(forward) &&
                    editor.UndoRedoManager.UndoActionCount == history,
                    "Alt-remapped key preserves text input: " + input.GetType().Name);
            }
            keyboardState.SetModifiers(ModifierKeys.None);
            SetBinding(KeyBindingDefinitions.KBD_QuickBulletDamageUp, Key.F13, ModifierKeys.None);

            foreach (var input in textInputs)
            {
                current = bullets.Select(x => x.BulletDamageTypeValue).ToArray();
                history = editor.UndoRedoManager.UndoActionCount;
                var textEvent = Event(Key.F13, input);
                input.RaiseEvent(textEvent);
                check(!textEvent.Handled && bullets.Select(x => x.BulletDamageTypeValue).SequenceEqual(current) && editor.UndoRedoManager.UndoActionCount == history,
                    "routed remapped key rejects text input source: " + input.GetType().Name);
                // The original Up/Down action guard is also tested independently of control class handlers.
                var originalDirection = Event(Key.Up, input);
                editor.KeyboardAction_QuickBulletDamageUp(new ActionExecutionContext { EventArgs = originalDirection });
                check(!originalDirection.Handled && editor.UndoRedoManager.UndoActionCount == history,
                    "direction-key action preserves text input: " + input.GetType().Name);
            }
            foreach (var bullet in bullets) bullet.IsSelected = false;
            routed = Event(Key.F13, view);
            history = editor.UndoRedoManager.UndoActionCount;
            view.RaiseEvent(routed);
            check(!routed.Handled && editor.UndoRedoManager.UndoActionCount == history && bell.IsSelected, "only Bell selected does not consume routed damage key");
            foreach (var bullet in bullets) bullet.IsSelected = true;
            editor.SwitchMode(true);
            routed = Event(Key.F13, view);
            view.RaiseEvent(routed);
            check(!routed.Handled && editor.UndoRedoManager.UndoActionCount == history, "non-design mode does not consume routed damage key");
            editor.SwitchMode(false);
            CheckSupport.SetViewport(editor);
            using (var other = CheckSupport.CreateEditor())
            {
                routed = Event(Key.F13, view);
                view.RaiseEvent(routed);
                check(!routed.Handled && editor.UndoRedoManager.UndoActionCount == history, "inactive editor does not consume routed damage key");
            }
            IoC.Get<IShell>().ActiveLayoutItem = editor;
            var batchMode = new BatchModeSwitchCommandHandler(IoC.Get<IEditorDocumentManager>());
            editor.ToggleBatchProjectilePlacement(pallete, false);
            batchMode.Run(null!).GetAwaiter().GetResult();
            try
            {
                check(editor.IsBatchMode && !editor.IsRequestPlaceBatchProjectilePreview && !editor.CanRequestBatchProjectilePlacement,
                    "entering batch mode cancels placement and prevents a competing session");
                editor.ToggleBatchProjectilePlacement(pallete, false);
                check(!editor.IsRequestPlaceBatchProjectilePreview, "placement cannot restart while batch mode owns input");
            }
            finally
            {
                if (editor.IsBatchMode)
                    batchMode.Run(null!).GetAwaiter().GetResult();
            }
            check(editor.CanRequestBatchProjectilePlacement && !editor.IsRequestPlaceBatchProjectilePreview,
                "leaving batch mode enables placement without resurrecting the old request");
            editor.CancelBatchProjectilePlacement();
            check(!view.IsVisible, "real editor view remained hidden during routed-input checks");
        }
        finally
        {
            foreach (var item in originalKeys)
                SetBinding(item.Definition, item.Key, item.Modifiers);
            root.Children.Remove(inputHost);
            view.DataContext = null;
        }
    }

    private sealed class DetachedPresentationSource : PresentationSource
    {
        public override Visual? RootVisual { get; set; }
        public override bool IsDisposed => false;
        protected override CompositionTarget? GetCompositionTargetCore() => null;
    }
}
