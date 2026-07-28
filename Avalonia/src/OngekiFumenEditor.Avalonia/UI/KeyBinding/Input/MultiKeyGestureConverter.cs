using Avalonia.Input;
using System.ComponentModel;
using System.Globalization;
using System.Text;

namespace OngekiFumenEditor.Avalonia.UI.KeyBinding.Input;

public class MultiKeyGestureConverter : TypeConverter
{
    public static readonly MultiKeyGestureConverter DefaultConverter = new();

    private static bool TryGetModifierKeys(string str, out KeyModifiers modifier)
    {
        switch (str.ToUpperInvariant())
        {
            case "CONTROL":
            case "CTRL":
                modifier = KeyModifiers.Control;
                return true;
            case "SHIFT":
                modifier = KeyModifiers.Shift;
                return true;
            case "ALT":
                modifier = KeyModifiers.Alt;
                return true;
            case "WINDOWS":
            case "WIN":
                modifier = KeyModifiers.Meta;
                return true;
            default:
                modifier = KeyModifiers.None;
                return false;
        }
    }

    public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
    {
        return sourceType == typeof(string);
    }

    public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
    {
        if (value is not string str || string.IsNullOrWhiteSpace(str))
            throw GetConvertFromException(value);

        var sequences = str.Split(',');
        var keySequences = new List<KeySequence>();

        foreach (var sequence in sequences)
        {
            var modifier = KeyModifiers.None;
            var keys = new List<Key>();
            var keyStrings = sequence.Split('+');
            var modifiersCount = 0;

            while (modifiersCount < keyStrings.Length &&
                   TryGetModifierKeys(keyStrings[modifiersCount].Trim(), out var currentModifier))
            {
                modifiersCount++;
                modifier |= currentModifier;
            }

            for (var i = modifiersCount; i < keyStrings.Length; i++)
            {
                var keyString = keyStrings[i].Trim();
                if (Enum.TryParse<Key>(keyString, true, out var key))
                    keys.Add(key);
            }

            keySequences.Add(new KeySequence(modifier, keys.ToArray()));
        }

        return new MultiKeyGesture(str, keySequences.ToArray());
    }

    public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value, Type destinationType)
    {
        if (destinationType == typeof(string) && value is MultiKeyGesture gesture)
        {
            var builder = new StringBuilder();
            for (var i = 0; i < gesture.KeySequences.Length; i++)
            {
                if (i > 0)
                    builder.Append(", ");
                builder.Append(gesture.KeySequences[i]);
            }
            return builder.ToString();
        }

        throw GetConvertToException(value, destinationType);
    }
}
