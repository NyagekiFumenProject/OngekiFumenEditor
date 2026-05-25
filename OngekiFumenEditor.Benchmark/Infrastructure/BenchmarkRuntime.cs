using System.Windows;
using Caliburn.Micro;
using OngekiFumenEditor.Parser;

namespace OngekiFumenEditor.Benchmark.Infrastructure;

internal static class BenchmarkRuntime
{
    private static readonly object Gate = new();
    private static App? app;
    private static AppBootstrapper? bootstrapper;
    private static bool initialized;

    public static void EnsureInitialized()
    {
        if (initialized)
            return;

        lock (Gate)
        {
            if (initialized)
                return;

            if (Application.Current is null)
                app = new App(false);
            else
                app = Application.Current as App;

            bootstrapper = new AppBootstrapper(false)
            {
                IsGUIMode = false
            };

            _ = IoC.Get<IFumenParserManager>();
            initialized = true;
        }
    }
}
