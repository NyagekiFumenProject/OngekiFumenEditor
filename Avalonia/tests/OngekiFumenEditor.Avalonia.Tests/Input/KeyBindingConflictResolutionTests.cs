using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Gekimini.Avalonia.Framework.Dialogs;
using Gekimini.Avalonia.Modules.Dialogs.ViewModels;
using OngekiFumenEditor.Avalonia.Assets.Languages;
using OngekiFumenEditor.Avalonia.Kernel.KeyBinding;
using OngekiFumenEditor.Avalonia.Kernel.SettingPages.KeyBinding.Dialogs;
using Xunit;

namespace OngekiFumenEditor.Avalonia.Tests.Input;

public sealed class KeyBindingConflictResolutionTests
{
    [Fact]
    public void ChangeKeyBindingResolvingConflicts_ClearsEveryApplicableConflict()
    {
        var target = Definition("target", Key.C, KeyBindingLayer.Normal);
        var normalConflict = Definition("normalConflict", Key.A, KeyBindingLayer.Normal);
        var globalConflict = Definition("globalConflict", Key.A, KeyBindingLayer.Global);
        var batchNonConflict = Definition("batchNonConflict", Key.A, KeyBindingLayer.Batch);
        var modifierNonConflict = Definition(
            "modifierNonConflict",
            Key.A,
            KeyBindingLayer.Normal,
            KeyModifiers.Control);
        var manager = new StubKeyBindingManager(
            target,
            normalConflict,
            globalConflict,
            batchNonConflict,
            modifierNonConflict);

        ((IKeyBindingManager)manager).ChangeKeyBindingResolvingConflicts(target, Key.A, KeyModifiers.None);

        Assert.Equal(Key.A, target.Key);
        Assert.Equal(Key.None, normalConflict.Key);
        Assert.Equal(Key.None, globalConflict.Key);
        Assert.Equal(Key.A, batchNonConflict.Key);
        Assert.Equal(Key.A, modifierNonConflict.Key);
        Assert.Equal(KeyModifiers.Control, modifierNonConflict.Modifiers);
        Assert.Equal(3, manager.ChangeCount);
        Assert.Equal(0, manager.SaveCount);
    }

    [Fact]
    public void ChangeKeyBindingResolvingConflicts_GlobalTargetClearsEveryLayer()
    {
        var target = Definition("target", Key.C, KeyBindingLayer.Global);
        var normalConflict = Definition("normalConflict", Key.A, KeyBindingLayer.Normal);
        var batchConflict = Definition("batchConflict", Key.A, KeyBindingLayer.Batch);
        var manager = new StubKeyBindingManager(target, normalConflict, batchConflict);

        ((IKeyBindingManager)manager).ChangeKeyBindingResolvingConflicts(target, Key.A, KeyModifiers.None);

        Assert.Equal(Key.A, target.Key);
        Assert.Equal(Key.None, normalConflict.Key);
        Assert.Equal(Key.None, batchConflict.Key);
    }

    [AvaloniaFact]
    public async Task TryApplyAsync_ConflictCanceled_DoesNotMutateBindings()
    {
        var target = Definition("target", Key.A, KeyBindingLayer.Normal);
        var conflict = Definition("conflict", Key.A, KeyBindingLayer.Normal);
        var manager = new StubKeyBindingManager(target, conflict);
        var dialogManager = new StubDialogManager(false);
        var dialog = new ConfigKeyBindingDialog(target, manager, dialogManager);

        var result = await dialog.TryApplyAsync();

        Assert.False(result);
        Assert.Equal(Key.A, target.Key);
        Assert.Equal(Key.A, conflict.Key);
        Assert.True(dialog.HasConflict);
        Assert.Same(conflict, dialog.ConflictDefinition);
        Assert.Equal(1, dialogManager.ConfirmCallCount);
        Assert.Contains(conflict.DisplayName, dialogManager.LastContent);
        Assert.Equal(Lang.Warning, dialogManager.LastTitle);
        Assert.Equal(0, manager.ChangeCount);
        Assert.Equal(0, manager.SaveCount);
    }

    [AvaloniaFact]
    public async Task TryApplyAsync_ConflictConfirmed_ClearsAllApplicableBindings()
    {
        var target = Definition("target", Key.A, KeyBindingLayer.Normal);
        var normalConflict = Definition("normalConflict", Key.A, KeyBindingLayer.Normal);
        var globalConflict = Definition("globalConflict", Key.A, KeyBindingLayer.Global);
        var batchNonConflict = Definition("batchNonConflict", Key.A, KeyBindingLayer.Batch);
        var manager = new StubKeyBindingManager(target, normalConflict, globalConflict, batchNonConflict);
        var dialogManager = new StubDialogManager(true);
        var dialog = new ConfigKeyBindingDialog(target, manager, dialogManager);

        var result = await dialog.TryApplyAsync();

        Assert.True(result);
        Assert.Equal(Key.A, target.Key);
        Assert.Equal(Key.None, normalConflict.Key);
        Assert.Equal(Key.None, globalConflict.Key);
        Assert.Equal(Key.A, batchNonConflict.Key);
        Assert.False(dialog.HasConflict);
        Assert.Null(dialog.ConflictDefinition);
        Assert.Equal(1, dialogManager.ConfirmCallCount);
        Assert.Equal(3, manager.ChangeCount);
        Assert.Equal(0, manager.SaveCount);
    }

    [AvaloniaFact]
    public async Task TryApplyAsync_NoConflict_AppliesWithoutConfirmation()
    {
        var target = Definition("target", Key.B, KeyBindingLayer.Normal);
        var other = Definition("other", Key.A, KeyBindingLayer.Normal);
        var manager = new StubKeyBindingManager(target, other);
        var dialogManager = new StubDialogManager(false);
        var dialog = new ConfigKeyBindingDialog(target, manager, dialogManager);

        var result = await dialog.TryApplyAsync();

        Assert.True(result);
        Assert.Equal(Key.B, target.Key);
        Assert.Equal(Key.A, other.Key);
        Assert.Equal(0, dialogManager.ConfirmCallCount);
        Assert.Equal(1, manager.ChangeCount);
    }

    private static KeyBindingDefinition Definition(
        string name,
        Key key,
        KeyBindingLayer layer,
        KeyModifiers modifiers = KeyModifiers.None)
    {
        return new KeyBindingDefinition(name, modifiers, key, layer);
    }

    private sealed class StubKeyBindingManager(params KeyBindingDefinition[] definitions) : IKeyBindingManager
    {
        public int ChangeCount { get; private set; }
        public int SaveCount { get; private set; }
        public IEnumerable<KeyBindingDefinition> KeyBindingDefinations => definitions;

        public Task Initialize() => Task.CompletedTask;

        public bool CheckKeyBinding(KeyBindingDefinition defination, KeyEventArgs e) => false;

        public void ChangeKeyBinding(
            KeyBindingDefinition definition,
            Key newKey,
            KeyModifiers newModifier)
        {
            ChangeCount++;
            definition.Key = newKey;
            definition.Modifiers = newModifier;
        }

        public KeyBindingDefinition QueryKeyBinding(
            Key key,
            KeyModifiers modifier,
            KeyBindingLayer layer)
        {
            return definitions.FirstOrDefault(x =>
                x.Key == key &&
                x.Modifiers == modifier &&
                (x.Layer == KeyBindingLayer.Global || layer == KeyBindingLayer.Global || x.Layer == layer))!;
        }

        public void SaveConfig()
        {
            SaveCount++;
        }

        public void LoadConfig()
        {
        }
    }

    private sealed class StubDialogManager(bool confirmResult) : IDialogManager
    {
        public int ConfirmCallCount { get; private set; }
        public string LastContent { get; private set; } = string.Empty;
        public string LastTitle { get; private set; } = string.Empty;

        public Task<T> ShowDialog<T>() where T : DialogViewModelBase => throw new NotSupportedException();

        public Task ShowDialog(DialogViewModelBase dialogViewModel) => throw new NotSupportedException();

        public Task ShowMessageDialog(
            string content,
            DialogMessageType messageType = DialogMessageType.Info) => throw new NotSupportedException();

        public Task<bool> ShowComfirmDialog(
            string content,
            string title = null!,
            string yesButtonContent = null!,
            string noButtonContent = null!)
        {
            ConfirmCallCount++;
            LastContent = content;
            LastTitle = title;
            return Task.FromResult(confirmResult);
        }
    }
}
