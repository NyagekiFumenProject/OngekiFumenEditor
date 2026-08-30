#nullable enable

using System.Text.Json;
using Avalonia.Input;
using Microsoft.Extensions.DependencyInjection;
using OngekiFumenEditor.Avalonia.Desktop.Platforms.Services.KeyBinding;
using OngekiFumenEditor.Avalonia.Kernel.KeyBinding;
using Xunit;

namespace OngekiFumenEditor.Avalonia.Desktop.Tests.KeyBinding;

public sealed class DesktopKeyBindingManagerTests
{
    [Fact]
    public void DefaultConfigFilePath_IsKeybindJsonBesideExecutable()
    {
        Assert.Equal(
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "keybind.json")),
            DesktopKeyBindingManager.DefaultConfigFilePath);
    }

    [Fact]
    public void DesktopRegistration_ProvidesPlatformKeyBindingManager()
    {
        var services = new ServiceCollection();
        services.AddOngekiFumenEditorAvaloniaDesktop();

        var descriptor = Assert.Single(
            services,
            service => service.ServiceType == typeof(IKeyBindingManager));

        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
        Assert.Equal(typeof(DesktopKeyBindingManager), descriptor.ImplementationType);
    }

    [Fact]
    public void SaveAndReload_PersistsBindingsAsUtf8JsonInConfiguredFile()
    {
        using var directory = new TemporaryDirectory();
        string path = Path.Combine(directory.RootPath, "keybind.json");
        var original = new KeyBindingDefinition("test.binding", KeyModifiers.Control, Key.A);
        var writer = new DesktopKeyBindingManager([original], path);

        original.Key = Key.B;
        original.Modifiers = KeyModifiers.Alt;
        writer.SaveConfig();

        Assert.True(File.Exists(path));
        byte[] bytes = File.ReadAllBytes(path);
        Assert.False(bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF);

        using (JsonDocument document = JsonDocument.Parse(bytes))
        {
            Assert.Equal(
                "Alt + B",
                document.RootElement.GetProperty("KeyBindings").GetProperty("test.binding").GetString());
        }

        var reloadedDefinition = new KeyBindingDefinition(
            "test.binding",
            KeyModifiers.Control,
            Key.A);
        var reader = new DesktopKeyBindingManager([reloadedDefinition], path);

        Assert.Equal(Key.B, reloadedDefinition.Key);
        Assert.Equal(KeyModifiers.Alt, reloadedDefinition.Modifiers);
        Assert.Equal(Path.GetFullPath(path), reader.ConfigFilePath);
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public string RootPath { get; } = Path.Combine(
            Path.GetTempPath(),
            "OngekiFumenEditor.DesktopKeyBindingManagerTests",
            Guid.NewGuid().ToString("N"));

        public TemporaryDirectory() => Directory.CreateDirectory(RootPath);

        public void Dispose()
        {
            if (Directory.Exists(RootPath))
                Directory.Delete(RootPath, recursive: true);
        }
    }
}
