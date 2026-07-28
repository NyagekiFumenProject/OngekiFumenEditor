using Avalonia.Input;
using System.Text;

namespace OngekiFumenEditor.Avalonia.UI.KeyBinding.Input;

public class KeySequence
{
    public Key[] Keys { get; }
    public KeyModifiers Modifiers { get; }

    public KeySequence(KeyModifiers modifiers, params Key[] keys)
    {
        if (keys is null)
            throw new ArgumentNullException(nameof(keys));
        if (keys.Length < 1)
            throw new ArgumentException("At least 1 key should be provided", nameof(keys));

        Keys = new Key[keys.Length];
        keys.CopyTo(Keys, 0);
        Modifiers = modifiers;
    }

    public override string ToString()
    {
        var builder = new StringBuilder();

        if (Modifiers != KeyModifiers.None)
        {
            if (Modifiers.HasFlag(KeyModifiers.Control))
                builder.Append("Ctrl+");
            if (Modifiers.HasFlag(KeyModifiers.Alt))
                builder.Append("Alt+");
            if (Modifiers.HasFlag(KeyModifiers.Shift))
                builder.Append("Shift+");
            if (Modifiers.HasFlag(KeyModifiers.Meta))
                builder.Append("Windows+");
        }

        builder.Append(Keys[0]);
        for (var i = 1; i < Keys.Length; i++)
            builder.Append("+" + Keys[i]);

        return builder.ToString();
    }
}
