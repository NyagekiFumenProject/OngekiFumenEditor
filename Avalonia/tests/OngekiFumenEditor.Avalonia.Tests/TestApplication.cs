using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text.Json.Serialization.Metadata;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Themes.Fluent;
using CommunityToolkit.Mvvm.Messaging;
using Gekimini.Avalonia.Framework.Dialogs;
using Gekimini.Avalonia.Platforms.Services.Settings;
using Gekimini.Avalonia.Utils.MethodExtensions;
using Microsoft.Extensions.DependencyInjection;
using OngekiFumenEditor.Avalonia.Kernel.KeyBinding;
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
        // headless 可能按测试重建 App 与 DI 容器，但 WeakReferenceMessenger 是进程级静态；
        // 不重置的话，历史 ShellViewModel（可能持有残留脏文档）会继续应答退出询问。
        WeakReferenceMessenger.Default.Reset();

        var services = new ServiceCollection();
        RegisterServices(services);
        services.AddOngekiFumenEditorAvalonia();
        services.AddSingleton<IKeyBindingManager, TestKeyBindingManager>();
        services.AddTypeCollectedActivator(
            global::OngekiFumenEditor.Avalonia.ViewTypeCollectedActivator.Default);
        services.AddTypeCollectedActivator(
            global::OngekiFumenEditor.Avalonia.ToolViewModelTypeCollectedActivator.Default);
        services.AddSingleton<ITemporaryFolderProvider, DiscardTemporaryFolderProvider>();
        services.AddSingleton<ISettingManager, InMemorySettingManager>();
        // 真实 DefaultDialogManager 在 headless 下会等待视图交互，用可编程替身覆盖（后注册生效）。
        services.AddSingleton<IDialogManager>(ProgrammableDialogManager.Instance);

        // Gekimini owns this field privately; populate it for headless tests while intentionally skipping shell startup.
        ServiceProviderField.SetValue(this, services.BuildServiceProvider());

        // 真实应用由 Gekimini 的 IThemeManager 在启动时把 FluentTheme 加进 Styles；
        // headless 路径跳过了那一启动流程，这里补上以保持与运行时一致。
        Styles.Add(new FluentTheme());
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

    private sealed class TestKeyBindingManager(IEnumerable<KeyBindingDefinition> definitions)
        : KeyBindingManagerBase(definitions)
    {
        public override Task Initialize() => Task.CompletedTask;

        public override void SaveConfig()
        {
        }

        public override void LoadConfig()
        {
        }
    }
}
