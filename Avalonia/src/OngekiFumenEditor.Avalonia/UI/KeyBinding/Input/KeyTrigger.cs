using Avalonia.Input;

namespace OngekiFumenEditor.Avalonia.UI.KeyBinding.Input;

public class KeyTrigger
{
    public Key Key { get; set; }
    public KeyModifiers Modifiers { get; set; }

    public event Action<KeyEventArgs> Triggered;

    public bool TryHandle(KeyEventArgs e)
    {
        if (e is null)
            return false;

        var key = e.Key;
        if (key == Key && e.KeyModifiers == GetActualModifiers(e.Key, Modifiers))
        {
            Triggered?.Invoke(e);
            return true;
        }

        return false;
    }

    private static KeyModifiers GetActualModifiers(Key key, KeyModifiers modifiers)
    {
        return key switch
        {
            Key.LeftCtrl or Key.RightCtrl => modifiers | KeyModifiers.Control,
            Key.LeftAlt or Key.RightAlt => modifiers | KeyModifiers.Alt,
            Key.LeftShift or Key.RightShift => modifiers | KeyModifiers.Shift,
            _ => modifiers
        };
    }
}
