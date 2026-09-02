using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization.Metadata;
using Avalonia.Controls.ApplicationLifetimes;
using CommunityToolkit.Mvvm.Messaging;
using Gekimini.Avalonia.Framework.Dialogs;
using Gekimini.Avalonia.Platforms.Services.Settings;
using Microsoft.Extensions.DependencyInjection;
using Gekimini.Avalonia.Utils.MethodExtensions;
using OngekiFumenEditor.Avalonia.Kernel.KeyBinding;
using OngekiFumenEditor.Avalonia.Platforms.Services.FileSystem.Providers;

namespace OngekiFumenEditor.Avalonia.Tests;

public sealed class TestApplication : global::OngekiFumenEditor.Avalonia.App
{

    public TestApplication() : base(isGUIMode: false)
    {
    }
    public override void Initialize()
    {
        // headless 可能按测试重建 App，但 WeakReferenceMessenger 是进程级静态；
        // 在构建 DI 和创建单例前清理历史 ShellViewModel 的退出应答。
        WeakReferenceMessenger.Default.Reset();
        ProgrammableDialogManager.Instance.Reset();
        base.Initialize();
    }

    public override void RegisterServices()
    {
        ApplicationLifetime ??= new ClassicDesktopStyleApplicationLifetime();
        base.RegisterServices();
    }

    protected override void RegisterServices(IServiceCollection services)
    {
        base.RegisterServices(services);

        // 注意：本类继承的是 OngekiFumenEditor.Avalonia.App（XAML 类），
        // 不经过 OngekiFumenEditorApp.RegisterServices，共享层注册必须在这里显式补齐。
        services.AddOngekiFumenEditorAvalonia();
        services.AddTypeCollectedActivator(
            global::OngekiFumenEditor.Avalonia.ViewTypeCollectedActivator.Default);
        services.AddTypeCollectedActivator(
            global::OngekiFumenEditor.Avalonia.ToolViewModelTypeCollectedActivator.Default);

        services.AddSingleton<IKeyBindingManager, TestKeyBindingManager>();
        services.AddSingleton<ITemporaryFolderProvider, DiscardTemporaryFolderProvider>();
        services.AddSingleton<ISettingManager, InMemorySettingManager>();
        // 真实 DefaultDialogManager 在 headless 下会等待视图交互，用可编程替身覆盖（后注册生效）。
        services.AddSingleton<IDialogManager>(ProgrammableDialogManager.Instance);
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
