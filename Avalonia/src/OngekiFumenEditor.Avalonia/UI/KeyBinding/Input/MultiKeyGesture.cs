using Avalonia.Input;
using System.ComponentModel;
using System.Text;

namespace OngekiFumenEditor.Avalonia.UI.KeyBinding.Input;

/// <summary>
/// Avalonia-compatible multi-key gesture matcher.
/// </summary>
[TypeConverter(typeof(MultiKeyGestureConverter))]
public class MultiKeyGesture
{
    private static readonly TimeSpan MaximumDelay = TimeSpan.FromSeconds(1);

    private readonly string displayString;
    private readonly KeySequence[] keySequences;

    private int currentKeyIndex;
    private int currentSequenceIndex;
    private DateTime lastKeyPress;

    public KeySequence[] KeySequences => keySequences;
    public string DisplayString => displayString;

    public MultiKeyGesture(params KeySequence[] sequences) : this(GetKeySequencesString(sequences), sequences)
    {
    }

    public MultiKeyGesture(string displayString, params KeySequence[] sequences)
    {
        if (sequences is null)
            throw new ArgumentNullException(nameof(sequences));
        if (sequences.Length == 0)
            throw new ArgumentException("At least one sequence must be specified.", nameof(sequences));

        this.displayString = displayString;
        keySequences = new KeySequence[sequences.Length];
        sequences.CopyTo(keySequences, 0);
    }

    public bool Matches(KeyEventArgs args)
    {
        if (args is null)
            return false;

        var key = args.Key;
        if (!IsDefinedKey(key))
            return false;
        if (IsModifierKey(key))
            return false;

        var currentSequence = keySequences[currentSequenceIndex];
        var currentKey = currentSequence.Keys[currentKeyIndex];

        if (currentSequenceIndex != 0 && DateTime.Now - lastKeyPress > MaximumDelay)
        {
            ResetState();
            return false;
        }

        if (currentSequence.Modifiers != args.KeyModifiers)
        {
            ResetState();
            return false;
        }

        if (currentKey != key)
        {
            ResetState();
            return false;
        }

        currentKeyIndex++;
        if (currentKeyIndex == keySequences[currentSequenceIndex].Keys.Length)
        {
            currentSequenceIndex++;
            currentKeyIndex = 0;
        }

        if (currentSequenceIndex != keySequences.Length)
        {
            lastKeyPress = DateTime.Now;
            args.Handled = true;
            return false;
        }

        ResetState();
        args.Handled = true;
        return true;
    }

    private static string GetKeySequencesString(params KeySequence[] sequences)
    {
        if (sequences is null)
            throw new ArgumentNullException(nameof(sequences));
        if (sequences.Length == 0)
            throw new ArgumentException("At least one sequence must be specified.", nameof(sequences));

        var builder = new StringBuilder();
        builder.Append(sequences[0]);
        for (var i = 1; i < sequences.Length; i++)
            builder.Append(", " + sequences[i]);
        return builder.ToString();
    }

    private static bool IsDefinedKey(Key key)
    {
        return key >= Key.None && key <= Key.OemClear;
    }

    private static bool IsModifierKey(Key key)
    {
        return key is Key.LeftCtrl or Key.RightCtrl or Key.LeftShift or Key.RightShift or Key.LeftAlt or Key.RightAlt or Key.LWin or Key.RWin;
    }

    private void ResetState()
    {
        currentSequenceIndex = 0;
        currentKeyIndex = 0;
    }
}
