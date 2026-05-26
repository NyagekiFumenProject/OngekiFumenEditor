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

            // 主项目用 Fody Costura 把依赖打包成嵌入资源;benchmark 进程也得显式 Attach,
            // 否则 IoC 解析时找不到部分嵌入程序集。
            var attach = typeof(App).Assembly.GetType("Costura.AssemblyLoader")?.GetMethod("Attach");
            attach?.Invoke(null, [true]);

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
