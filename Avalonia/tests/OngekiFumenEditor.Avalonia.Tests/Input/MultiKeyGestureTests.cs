using Avalonia.Input;
using OngekiFumenEditor.Avalonia.UI.KeyBinding.Input;
using Xunit;

namespace OngekiFumenEditor.Avalonia.Tests.Input;

public sealed class MultiKeyGestureTests
{
    [Fact]
    public void Matches_MultiStepSequence_HandlesIntermediateAndFinalEvents()
    {
        var gesture = new MultiKeyGesture(
            new KeySequence(KeyModifiers.Control, Key.K),
            new KeySequence(KeyModifiers.Control, Key.C));
        var first = KeyEvent(Key.K, KeyModifiers.Control);
        var second = KeyEvent(Key.C, KeyModifiers.Control);

        var firstMatched = gesture.Matches(first);
        var secondMatched = gesture.Matches(second);

        Assert.False(firstMatched);
        Assert.True(first.Handled);
        Assert.True(secondMatched);
        Assert.True(second.Handled);
        Assert.Equal("Ctrl+K, Ctrl+C", gesture.DisplayString);
    }

    [Fact]
    public void Matches_WrongKey_ResetsSequenceAndLeavesWrongEventUnhandled()
    {
        var gesture = new MultiKeyGesture(
            new KeySequence(KeyModifiers.Control, Key.K),
            new KeySequence(KeyModifiers.Control, Key.C));
        var first = KeyEvent(Key.K, KeyModifiers.Control);
        var wrong = KeyEvent(Key.X, KeyModifiers.Control);
        var formerFinal = KeyEvent(Key.C, KeyModifiers.Control);

        Assert.False(gesture.Matches(first));
        Assert.False(gesture.Matches(wrong));
        Assert.False(gesture.Matches(formerFinal));
        Assert.True(first.Handled);
        Assert.False(wrong.Handled);
        Assert.False(formerFinal.Handled);
    }

    [Fact]
    public void Matches_WrongModifiers_ResetSequence()
    {
        var gesture = new MultiKeyGesture(
            new KeySequence(KeyModifiers.Control, Key.K),
            new KeySequence(KeyModifiers.Control, Key.C));

        Assert.False(gesture.Matches(KeyEvent(Key.K, KeyModifiers.Control)));
        Assert.False(gesture.Matches(KeyEvent(Key.C, KeyModifiers.Alt)));

        var restartedFirst = KeyEvent(Key.K, KeyModifiers.Control);
        var restartedFinal = KeyEvent(Key.C, KeyModifiers.Control);
        Assert.False(gesture.Matches(restartedFirst));
        Assert.True(gesture.Matches(restartedFinal));
        Assert.True(restartedFirst.Handled);
        Assert.True(restartedFinal.Handled);
    }

    [Fact]
    public void Matches_ModifierKey_DoesNotAdvanceOrResetSequence()
    {
        var gesture = new MultiKeyGesture(
            new KeySequence(KeyModifiers.Control, Key.K),
            new KeySequence(KeyModifiers.Control, Key.C));
        var first = KeyEvent(Key.K, KeyModifiers.Control);
        var modifierOnly = KeyEvent(Key.LeftCtrl, KeyModifiers.Control);
        var final = KeyEvent(Key.C, KeyModifiers.Control);

        Assert.False(gesture.Matches(first));
        Assert.False(gesture.Matches(modifierOnly));
        Assert.True(gesture.Matches(final));
        Assert.True(first.Handled);
        Assert.False(modifierOnly.Handled);
        Assert.True(final.Handled);
    }

    [Fact]
    public void KeySequence_ToString_UsesDeterministicCrossPlatformModifierNames()
    {
        var sequence = new KeySequence(
            KeyModifiers.Control | KeyModifiers.Alt | KeyModifiers.Shift | KeyModifiers.Meta,
            Key.K,
            Key.C);

        Assert.Equal("Ctrl+Alt+Shift+Windows+K+C", sequence.ToString());
    }

    [Fact]
    public void Constructor_RejectsMissingSequences()
    {
        Assert.Throws<ArgumentException>(() => new MultiKeyGesture());
        Assert.Throws<ArgumentNullException>(() => new MultiKeyGesture((KeySequence[])null!));
        Assert.Throws<ArgumentException>(() => new KeySequence(KeyModifiers.None));
    }

    private static KeyEventArgs KeyEvent(Key key, KeyModifiers modifiers)
    {
        return new KeyEventArgs
        {
            Key = key,
            KeyModifiers = modifiers
        };
    }
}
