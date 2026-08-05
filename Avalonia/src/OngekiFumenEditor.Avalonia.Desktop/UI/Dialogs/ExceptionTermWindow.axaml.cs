using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using OngekiFumenEditor.Avalonia.Utils;
using System.Reflection;

namespace OngekiFumenEditor.Avalonia.Desktop.UI.Dialogs;

public partial class ExceptionTermWindow : Window
{
    private bool closeConfirmed;

    public string ExceptionMessage { get; init; }
    public string[] RescueFolderPaths { get; init; }
    public string LogFile { get; init; }
    public string DumpFile { get; init; }

    public string ProgramVersion { get; } = GetProgramVersion();

    public ExceptionTermWindow(string exceptionMessage, string[] rescueFolderPaths, string logFile, string dumpFile)
    {
        ExceptionMessage = exceptionMessage ?? string.Empty;
        RescueFolderPaths = rescueFolderPaths ?? [];
        LogFile = logFile ?? string.Empty;
        DumpFile = dumpFile ?? string.Empty;

        InitializeComponent();
        DataContext = this;
        WireUpEvents();
    }

    public ExceptionTermWindow()
        : this(string.Empty, [], string.Empty, string.Empty)
    {
    }

    public void OpenPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return;

        ProcessUtils.OpenExplorerToBrowser(path);
    }

    private void WireUpEvents()
    {
        CloseButton.Click += OnCloseButtonClick;
        Closing += OnClosing;
        // 救援目录链接在 ItemsControl 的 DataTemplate 里，构造函数中按 x:Name 找不到实例，
        // 三个链接统一用窗口层的 PointerPressed 冒泡事件按 x:Name 过滤（对应 WPF 的 Hyperlink_Click）。
        AddHandler(InputElement.PointerPressedEvent, OnPathLinkPointerPressed, RoutingStrategies.Bubble);
    }

    private void OnCloseButtonClick(object sender, RoutedEventArgs e)
    {
        closeConfirmed = true;
        Close();
    }

    private void OnClosing(object sender, WindowClosingEventArgs e)
    {
        if (!closeConfirmed)
            e.Cancel = true;
    }

    private void OnPathLinkPointerPressed(object sender, PointerPressedEventArgs e)
    {
        if (e.Source is not TextBlock link)
            return;

        var path = link.Name switch
        {
            "RescuePathLink" => link.DataContext as string,
            "LogFileLink" => LogFile,
            "DumpFileLink" => DumpFile,
            _ => null
        };
        if (string.IsNullOrWhiteSpace(path))
            return;

        OpenPath(path);
        e.Handled = true;
    }

    private static string GetProgramVersion()
    {
        var assembly = Assembly.GetEntryAssembly() ?? typeof(ExceptionTermWindow).Assembly;
        return assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
               ?? assembly.GetName().Version?.ToString()
               ?? string.Empty;
    }
}
