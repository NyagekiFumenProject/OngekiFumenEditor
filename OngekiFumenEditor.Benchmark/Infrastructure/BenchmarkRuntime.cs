using System.Windows;
using Caliburn.Micro;
using OngekiFumenEditor.Parser;

namespace OngekiFumenEditor.Benchmark.Infrastructure;

/// <summary>
/// 一次性引导 OngekiFumenEditor 的 WPF App 与 MEF/IoC 容器。
/// 参考 OngekiFumenEditor.CommandLine/Program.cs 的 bootstrap 流程:
/// new App(false) → new AppBootstrapper(false) → IoC.Get&lt;T&gt;() 即可使用 MEF 服务。
/// </summary>
internal static class BenchmarkRuntime
{
    private static readonly object gate = new();
    private static App? app;
    private static AppBootstrapper? bootstrapper;
    private static bool initialized;

    public static void EnsureInitialized()
    {
        if (initialized)
            return;

        lock (gate)
        {
            if (initialized)
                return;

            // 主项目使用 MSBuild PublishSingleFile 替代 Costura, 已无需手动 Attach 嵌入程序集。
            if (Application.Current is null)
                app = new App(false);
            else
                app = Application.Current as App;

            bootstrapper = new AppBootstrapper(false)
            {
                IsGUIMode = false
            };

            // 立刻触发一次 IoC 解析,提前发现 MEF 装配问题。
            _ = IoC.Get<IFumenParserManager>();
            initialized = true;
        }
    }
}
