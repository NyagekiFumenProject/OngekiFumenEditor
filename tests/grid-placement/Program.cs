using System;

internal static class Program
{
    [STAThread]
    private static int Main()
    {
        try
        {
            OngekiFumenEditor.Properties.EditorGlobalSetting.Default.SettingsSaving += (_, e) => e.Cancel = true;
            var count = GridClipboardChecks.Run();
            ObjectComponentsChecks.Run((condition, name) =>
            {
                if (!condition)
                    throw new InvalidOperationException(name);
                count++;
            });
            ArchitectureChecks.Run((condition, name) =>
            {
                if (!condition)
                    throw new InvalidOperationException(name);
                count++;
            });
            var switchFailures = new System.Collections.Generic.List<string>();
            LaneSwitchChecks.Run((condition, name) =>
            {
                if (!condition)
                    switchFailures.Add(name);
                count++;
            });
            if (switchFailures.Count > 0)
                throw new InvalidOperationException(string.Join(Environment.NewLine, switchFailures));
            var cacheFailures = new System.Collections.Generic.List<string>();
            StartCurveCacheChecks.Run((condition, name) =>
            {
                if (!condition)
                    cacheFailures.Add(name);
                count++;
            });
            if (cacheFailures.Count > 0)
                throw new InvalidOperationException(string.Join(Environment.NewLine, cacheFailures));
            GridInteractionChecks.Run((condition, name) =>
            {
                if (!condition)
                    throw new InvalidOperationException(name);
                count++;
            });
            ClipboardInteractionChecks.Run((condition, name) =>
            {
                if (!condition)
                    throw new InvalidOperationException(name);
                count++;
            });
            SelectionGridDragChecks.Run((condition, name) =>
            {
                if (!condition)
                    throw new InvalidOperationException(name);
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
