using System.Globalization;
using System.IO;
using System.Windows.Threading;
using Caliburn.Micro;

namespace OngekiFumenEditor.OutputPaneCheck;

/// <summary>
/// Checks the output pane of the editor (Gemini.Modules.Output) against a real window, dispatcher
/// and TextBox: auto scroll (自动滑动), clear (清理) and log append throughput.
///
/// usage: dotnet run --project OngekiFumenEditor.OutputPaneCheck -c Release -- [--lines N] [--log path] [--no-perf]
///
/// The window is shown off-screen, so a desktop session is required. Exit code 1 means a check failed.
/// </summary>
internal static class Program
{
    private const int DefaultLines = 2000;

    [STAThread]
    private static int Main(string[] args)
    {
        var lines = DefaultLines;
        string? logFile = null;
        var runPerf = true;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--lines":
                    lines = int.Parse(args[++i], CultureInfo.InvariantCulture);
                    break;
                case "--log":
                    logFile = args[++i];
                    break;
                case "--no-perf":
                    runPerf = false;
                    break;
                case "-h" or "--help":
                    PrintUsage();
                    return 0;
                default:
                    Console.WriteLine($"unknown argument: {args[i]}");
                    PrintUsage();
                    return 2;
            }
        }

        // The editor boots Caliburn's WPF platform provider, and the pane relies on it for
        // Execute.OnUIThread / Execute.BeginOnUIThread.
        PlatformProvider.Current = new XamlPlatformProvider();

        var host = new OutputPaneHost(BuildCorpus(logFile, lines), "OngekiFumenEditor output pane check");
        var checks = new OutputPaneChecks(host, lines, runPerf);
        var crashed = false;

        host.Window.Loaded += async (_, _) =>
        {
            try
            {
                await checks.RunAsync();
            }
            catch (Exception e)
            {
                crashed = true;
                Console.WriteLine($"check run crashed: {e}");
            }
            finally
            {
                host.Window.Dispatcher.InvokeShutdown();
            }
        };

        host.Window.Show();
        Dispatcher.Run();

        return crashed || checks.Failures > 0 ? 1 : 0;
    }

    private static string[] BuildCorpus(string? logFile, int lines)
    {
        if (logFile != null)
        {
            var fromFile = File.ReadAllLines(logFile).Where(x => x.Length > 0).Take(lines).ToArray();
            if (fromFile.Length > 0)
                return fromFile;

            Console.WriteLine($"log file {logFile} has no usable lines, falling back to generated lines");
        }

        // Roughly the shape of a real editor log line (~200 chars) so layout costs stay comparable.
        var payload = new string('x', 150);
        return Enumerable.Range(0, lines)
            .Select(i => $"[2026-01-01 00:00:{i % 60:00}.{i % 1000:000} INFO:{i % 64}]<Write:{i % 200}> log line {i} {payload}")
            .ToArray();
    }

    private static void PrintUsage()
    {
        Console.WriteLine("""
            usage: dotnet run --project OngekiFumenEditor.OutputPaneCheck -c Release -- [options]

              --lines N    log lines to use (default 2000)
              --log path   use the lines of a real log file instead of generated ones
              --no-perf    skip the throughput measurement
            """);
    }
}
