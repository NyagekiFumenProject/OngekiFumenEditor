using System;

internal static class Program
{
    [STAThread]
    private static int Main()
    {
        try
        {
            var count = GridClipboardChecks.Run();
            var cacheFailures = new System.Collections.Generic.List<string>();
            StartCurveCacheChecks.Run((condition, name) => { if (!condition) cacheFailures.Add(name); count++; });
            if (cacheFailures.Count > 0) throw new InvalidOperationException(string.Join(Environment.NewLine, cacheFailures));
            GridInteractionChecks.Run((condition, name) => { if (!condition) throw new InvalidOperationException(name); count++; });
            ClipboardInteractionChecks.Run((condition, name) => { if (!condition) throw new InvalidOperationException(name); count++; });
            SelectionGridDragChecks.Run((condition, name) =>
            {
                if (!condition) throw new InvalidOperationException(name);
                count++;
            });
            Console.WriteLine($"通过 {count} 项 Grid 放置回归检查。");
            return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error);
            return 1;
        }
    }
}
