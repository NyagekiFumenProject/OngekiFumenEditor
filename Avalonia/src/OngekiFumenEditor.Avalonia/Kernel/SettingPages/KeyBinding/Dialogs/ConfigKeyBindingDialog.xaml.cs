using Avalonia.Controls;
using Avalonia.Input;
using OngekiFumenEditor.Avalonia.Kernel.KeyBinding;
using OngekiFumenEditor.Avalonia.Assets.Languages;
using OngekiFumenEditor.Avalonia.Utils;
using System.ComponentModel;

namespace OngekiFumenEditor.Avalonia.Kernel.SettingPages.KeyBinding.Dialogs;

public partial class ConfigKeyBindingDialog : Window, INotifyPropertyChanged
{
    public KeyBindingDefinition Definition { get; }

    private KeyModifiers modifier;
    private Key key;

    public new event PropertyChangedEventHandler PropertyChanged;

    public string CurrentExpression => KeyBindingDefinition.FormatToExpression(key, modifier);

    public KeyBindingDefinition ConflictDefinition
    {
        get => field;
        private set
        {
            if (field == value)
                return;
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ConflictDefinition)));
        }
    }

    public ConfigKeyBindingDialog(KeyBindingDefinition definition)
    {
        Definition = definition;
        key = definition.Key;
        modifier = definition.Modifiers;

        KeyDown += OnKeyDown;
        KeyUp += OnKeyUp;
    }

    public ConfigKeyBindingDialog()
        : this(new KeyBindingDefinition("kbd_none", Key.None))
    {
    }

    private void OnKeyUp(object sender, KeyEventArgs e)
    {
        if (!IsActive)
            return;

        var inputKey = e.Key;
        if (Definition.Key == Key.None)
            TryClearModifier(inputKey);

        UpdateExpression();
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (!IsActive)
            return;

        var inputKey = e.Key;
        if (TryGetModifier(inputKey, out var inputModifier))
        {
            modifier = inputModifier;
            key = Key.None;
        }
        else
        {
            key = inputKey;
        }

        UpdateExpression();
    }

    private static bool TryGetModifier(Key key, out KeyModifiers modifier)
    {
        switch (key)
        {
            case Key.LeftCtrl:
            case Key.RightCtrl:
                modifier = KeyModifiers.Control;
                return true;
            case Key.LeftShift:
            case Key.RightShift:
                modifier = KeyModifiers.Shift;
                return true;
            case Key.LeftAlt:
            case Key.RightAlt:
                modifier = KeyModifiers.Alt;
                return true;
            case Key.LWin:
            case Key.RWin:
                modifier = KeyModifiers.Meta;
                return true;
            default:
                modifier = KeyModifiers.None;
                return false;
        }
    }

    private bool TryClearModifier(Key key)
    {
        switch (key)
        {
            case Key.LeftCtrl:
            case Key.RightCtrl:
            case Key.LeftShift:
            case Key.RightShift:
            case Key.LeftAlt:
            case Key.RightAlt:
            case Key.LWin:
            case Key.RWin:
                modifier = KeyModifiers.None;
                return true;
            default:
                return false;
        }
    }

    public void Clear()
    {
        key = Key.None;
        modifier = KeyModifiers.None;
        UpdateExpression();
    }

    public void ApplyDefault()
    {
        key = Definition.DefaultKey;
        modifier = Definition.DefaultModifiers;
        UpdateExpression();
    }

    public bool TryApply()
    {
        if (ConflictDefinition is not null)
        {
            Log.LogWarn(Lang.ConflictNotifyComfirm.Format(ConflictDefinition.DisplayName));
            return false;
        }

        Definition.Key = key;
        Definition.Modifiers = modifier;
        return true;
    }

    private void UpdateExpression()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentExpression)));

        if (string.IsNullOrWhiteSpace(CurrentExpression))
            return;

        var manager = IoC.Get<IKeyBindingManager>();
        var conflict = manager?.QueryKeyBinding(key, modifier, Definition.Layer);
        if (conflict == Definition)
            conflict = null;
        ConflictDefinition = conflict;
    }
}

