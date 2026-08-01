using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text.Json.Serialization.Metadata;
using Gekimini.Avalonia.Platforms.Services.Settings;
using Microsoft.Extensions.DependencyInjection;

namespace OngekiFumenEditor.Avalonia.Tests;

public sealed class TestApplication : global::OngekiFumenEditor.Avalonia.Avalonia.App
{
    private static readonly FieldInfo ServiceProviderField = typeof(global::Gekimini.Avalonia.App)
        .GetField("serviceProvider", BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new MissingFieldException(typeof(global::Gekimini.Avalonia.App).FullName, "serviceProvider");

    public TestApplication() : base(isGUIMode: false)
    {
    }

    public override void OnFrameworkInitializationCompleted()
    {
        var services = new ServiceCollection();
        RegisterServices(services);
        services.AddOngekiFumenEditorAvalonia();
        services.AddSingleton<ISettingManager, InMemorySettingManager>();

        // Gekimini owns this field privately; populate it for headless tests while intentionally skipping shell startup.
        ServiceProviderField.SetValue(this, services.BuildServiceProvider());
    }

    protected override void DoExit(int exitCode = 0)
    {
    }

    private sealed class InMemorySettingManager : ISettingManager
    {
        private readonly ConcurrentDictionary<Type, object> values = new();

        public void SaveSetting<T>(T obj, JsonTypeInfo<T> jsonTypeInfo)
        {
            ArgumentNullException.ThrowIfNull(obj);
            values[typeof(T)] = obj;
        }

        public T GetSetting<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] T>(
            JsonTypeInfo<T> jsonTypeInfo) where T : new()
        {
            return (T)values.GetOrAdd(typeof(T), static _ => new T());
        }
    }
}
