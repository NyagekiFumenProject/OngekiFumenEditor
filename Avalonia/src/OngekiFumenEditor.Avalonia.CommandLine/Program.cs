using System;

namespace OngekiFumenEditor.Avalonia.CommandLine;

/// <summary>
/// Command-line entry point, mirroring the original OngekiFumenEditor.CommandLine.exe that ships
/// next to the desktop GUI executable. The command executor itself has not been migrated yet, so
/// every invocation currently reports that no commands are available and exits with a non-zero
/// code to keep scripts from treating the run as a success.
/// </summary>
internal static class Program
{
    private const int NotImplementedExitCode = 1;

    private static int Main(string[] args)
    {
        var previousColor = Console.ForegroundColor;
        try
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine(
                "OngekiFumenEditor command line: no commands are available yet (the command executor has not been migrated).");
            return NotImplementedExitCode;
        }
        finally
        {
            Console.ForegroundColor = previousColor;
        }
    }
}
