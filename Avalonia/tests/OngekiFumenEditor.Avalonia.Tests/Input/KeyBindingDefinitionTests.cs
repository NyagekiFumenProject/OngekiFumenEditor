using Avalonia.Data.Converters;
using Avalonia.Input;
using OngekiFumenEditor.Avalonia.Kernel.KeyBinding;
using OngekiFumenEditor.Avalonia.Kernel.SettingPages.KeyBinding.ValueConverters;
using System.Globalization;
using Xunit;

namespace OngekiFumenEditor.Avalonia.Tests.Input;

public sealed class KeyBindingDefinitionTests
{
    [Fact]
    public void ShowKeybindExpressionValueConverter_MultiBindingContract_FormatsKeyAndModifiers()
    {
        var converter = new ShowKeybindExpressionValueConverter();
        var multiValueConverter = Assert.IsAssignableFrom<IMultiValueConverter>(converter);

        var result = multiValueConverter.Convert(
            [Key.PageUp, KeyModifiers.Control],
            typeof(string),
            null!,
            CultureInfo.InvariantCulture);

        Assert.Equal("Ctrl + PageUp", result);
    }

    [Theory]
    [InlineData(Key.A, KeyModifiers.None, "A")]
    [InlineData(Key.D1, KeyModifiers.Alt, "Alt + D1")]
    [InlineData(Key.PageUp, KeyModifiers.Control, "Ctrl + PageUp")]
    [InlineData(Key.F, KeyModifiers.Shift, "Shift + F")]
    [InlineData(Key.Z, KeyModifiers.Meta, "Win + Z")]
    public void FormatAndParse_SupportedModifiers_RoundTripsExactValues(
        Key key,
        KeyModifiers modifiers,
        string expectedExpression)
    {
        var expression = KeyBindingDefinition.FormatToExpression(key, modifiers);
        var parsed = KeyBindingDefinition.TryParseExpression(expression, out var parsedKey, out var parsedModifiers);

        Assert.Equal(expectedExpression, expression);
        Assert.True(parsed);
        Assert.Equal(key, parsedKey);
        Assert.Equal(modifiers, parsedModifiers);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData(null)]
    public void TryParseExpression_BlankValue_RepresentsNoBinding(string? expression)
    {
        var parsed = KeyBindingDefinition.TryParseExpression(expression!, out var key, out var modifiers);

        Assert.True(parsed);
        Assert.Equal(Key.None, key);
        Assert.Equal(KeyModifiers.None, modifiers);
    }

    [Theory]
    [InlineData("Hyper + A")]
    [InlineData("Ctrl + DefinitelyNotAKey")]
    [InlineData("+")]
    public void TryParseExpression_InvalidModifierOrKey_ReturnsFalse(string expression)
    {
        var parsed = KeyBindingDefinition.TryParseExpression(expression, out _, out _);

        Assert.False(parsed);
    }

    [Fact]
    public void InstanceFormat_UsesChangedValuesWithoutMutatingDefaults()
    {
        var definition = new KeyBindingDefinition("test", KeyModifiers.Control, Key.A);

        definition.Key = Key.B;
        definition.Modifiers = KeyModifiers.Alt;

        Assert.Equal("Alt + B", KeyBindingDefinition.FormatToExpression(definition));
        Assert.Equal(Key.A, definition.DefaultKey);
        Assert.Equal(KeyModifiers.Control, definition.DefaultModifiers);
    }
}
