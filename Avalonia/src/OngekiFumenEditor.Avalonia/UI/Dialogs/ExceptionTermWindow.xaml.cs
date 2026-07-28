using Avalonia.Controls;
using OngekiFumenEditor.Avalonia.Utils;
using System.Diagnostics;

namespace OngekiFumenEditor.Avalonia.UI.Dialogs;

public partial class ExceptionTermWindow : Window
{
    public string ExceptionMessage { get; init; }
    public string[] RescueFolderPaths { get; init; }
    public string LogFile { get; init; }
    public string DumpFile { get; init; }

    public string ProgramVersion => FileVersionInfo.GetVersionInfo(typeof(AppBootstrapper).Assembly.Location).ProductVersion;

    public ExceptionTermWindow(string exceptionMessage, string[] rescueFolderPaths, string logFile, string dumpFile)
    {
        ExceptionMessage = exceptionMessage;
        RescueFolderPaths = rescueFolderPaths ?? [];
        LogFile = logFile;
        DumpFile = dumpFile;

        DataContext = this;
    }

    public ExceptionTermWindow()
    {
        ExceptionMessage = string.Empty;
        RescueFolderPaths = [];
        LogFile = string.Empty;
        DumpFile = string.Empty;
        DataContext = this;
    }

    public void OpenPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return;
        ProcessUtils.OpenExplorerToBrowser(path);
    }
}
