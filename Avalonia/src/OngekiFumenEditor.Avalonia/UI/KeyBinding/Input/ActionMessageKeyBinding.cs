using Avalonia.Input;
using OngekiFumenEditor.Avalonia.Kernel.KeyBinding;
using OngekiFumenEditor.Avalonia.Utils;

namespace OngekiFumenEditor.Avalonia.UI.KeyBinding.Input;

public class ActionMessageKeyBinding
{
    public KeyBindingDefinition Definition { get; set; }
    public string Message { get; set; } = string.Empty;
    public bool IsEnable { get; set; } = true;

    public event Action<KeyEventArgs, string> ActionInvoked;

    public bool TryHandle(KeyEventArgs e)
    {
        if (!IsEnable || Definition is not KeyBindingDefinition def)
            return false;
        if (!IoC.Get<IKeyBindingManager>().CheckKeyBinding(def, e))
            return false;

        ActionInvoked?.Invoke(e, Message);
        if (def.Modifiers.HasFlag(KeyModifiers.Alt))
            e.Handled = true;
        return true;
    }
}
