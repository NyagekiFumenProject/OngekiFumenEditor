using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text.Json.Serialization.Metadata;
using Avalonia.Controls.ApplicationLifetimes;
using Gekimini.Avalonia.Platforms.Services.Settings;
using Gekimini.Avalonia.Utils.MethodExtensions;
using Microsoft.Extensions.DependencyInjection;
using OngekiFumenEditor.Avalonia.Platforms.Services.FileSystem.Providers;

namespace OngekiFumenEditor.Avalonia.Tests;

public sealed class TestApplication : global::OngekiFumenEditor.Avalonia.App
{
    private static readonly FieldInfo ServiceProviderField = typeof(global::Gekimini.Avalonia.App)
        .GetField("serviceProvider", BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new MissingFieldException(typeof(global::Gekimini.Avalonia.App).FullName, "serviceProvider");

    public TestApplication() : base(isGUIMode: false)
    {
    }

    public override void RegisterServices()
    {
        ApplicationLifetime ??= new ClassicDesktopStyleApplicationLifetime();
        base.RegisterServices();
    }

    public override void OnFrameworkInitializationCompleted()
    {
        var services = new ServiceCollection();
        RegisterServices(services);
        services.AddOngekiFumenEditorAvalonia();
        services.AddTypeCollectedActivator(
            global::OngekiFumenEditor.Avalonia.ViewTypeCollectedActivator.Default);
        services.AddTypeCollectedActivator(
            global::OngekiFumenEditor.Avalonia.ToolViewModelTypeCollectedActivator.Default);
        services.AddSingleton<ITemporaryFolderProvider, DiscardTemporaryFolderProvider>();
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
