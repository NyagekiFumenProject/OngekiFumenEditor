using Avalonia.Input;
using OngekiFumenEditor.Avalonia.Kernel.KeyBinding;
using OngekiFumenEditor.Avalonia.Utils;

namespace OngekiFumenEditor.Avalonia.UI.KeyBinding.Input;

public class KeyBinding
{
    public KeyBindingDefinition Definition { get; set; }

    public event Action<KeyEventArgs> Triggered;

    public bool TryHandle(KeyEventArgs e)
    {
        if (Definition is not KeyBindingDefinition def)
            return false;
        if (!IoC.Get<IKeyBindingManager>().CheckKeyBinding(def, e))
            return false;

        Triggered?.Invoke(e);
        return true;
    }
}
