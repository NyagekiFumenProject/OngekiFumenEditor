using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization.Metadata;
using Avalonia;
using Avalonia.Headless;
using Gekimini.Avalonia.Platforms.Services.Settings;
using Microsoft.Extensions.DependencyInjection;
using OngekiFumenEditor.Avalonia;
using OngekiFumenEditor.Avalonia.Parser;

namespace OngekiFumenEditor.Avalonia.Benchmark.Infrastructure;

/// <summary>
/// 在不创建窗口的前提下初始化 Avalonia 应用和共享服务容器。
/// </summary>
internal static class BenchmarkRuntime
{
    private static readonly object Gate = new();
    private static IServiceProvider? services;

    public static IServiceProvider Services =>
        services ?? throw new InvalidOperationException("Benchmark runtime has not been initialized.");

    public static void EnsureInitialized()
    {
        if (services is not null)
            return;

        lock (Gate)
        {
            if (services is not null)
                return;

            AppBuilder.Configure<BenchmarkApplication>()
                .UseHeadless(new AvaloniaHeadlessPlatformOptions
                {
                    UseHeadlessDrawing = true,
                    ShouldRenderOnUIThread = true
                })
                .UseSkia()
                .UseHarfBuzz()
                .SetupWithoutStarting();

            var app = Application.Current as BenchmarkApplication
                ?? throw new InvalidOperationException("Avalonia benchmark application was not created.");
            services = app.ServiceProvider;

            // Resolve the parser manager once so registration/reflection failures surface before measurement.
            _ = Services.GetRequiredService<IFumenParserManager>();
        }
    }
}

public sealed class BenchmarkApplication : OngekiFumenEditorApp
{
    public BenchmarkApplication()
        : base(isGUIMode: false)
    {
    }

    protected override void RegisterServices(IServiceCollection services)
    {
        base.RegisterServices(services);
        services.AddSingleton<ISettingManager, InMemorySettingManager>();
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
            => (T)values.GetOrAdd(typeof(T), static _ => new T());
    }

    protected override void DoExit(int exitCode = 0)
    {
    }
}
