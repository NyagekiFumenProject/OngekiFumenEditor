using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using OngekiFumenEditor.Avalonia.Avalonia;
using OngekiFumenEditor.Avalonia.Utils;
using System.Diagnostics;

namespace OngekiFumenEditor.Avalonia.UI.Dialogs;

public partial class ExceptionTermWindow : Window
{
    public string ExceptionMessage { get; init; }
    public string[] RescueFolderPaths { get; init; }
    public string LogFile { get; init; }
    public string DumpFile { get; init; }

    public string ProgramVersion => FileVersionInfo.GetVersionInfo(typeof(App).Assembly.Location).ProductVersion;

    public ExceptionTermWindow(string exceptionMessage, string[] rescueFolderPaths, string logFile, string dumpFile)
    {
        ExceptionMessage = exceptionMessage;
        RescueFolderPaths = rescueFolderPaths ?? [];
        LogFile = logFile;
        DumpFile = dumpFile;

        InitializeComponent();
        DataContext = this;
        WireUpEvents();
    }

    public ExceptionTermWindow()
    {
        ExceptionMessage = string.Empty;
        RescueFolderPaths = [];
        LogFile = string.Empty;
        DumpFile = string.Empty;

        InitializeComponent();
        DataContext = this;
        WireUpEvents();
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
        // 救援目录链接在 ItemsControl 的 DataTemplate 里，构造函数中按 x:Name 找不到实例，
        // 三个链接统一用窗口层的 PointerPressed 冒泡事件按 x:Name 过滤（对应 WPF 的 Hyperlink_Click）。
        AddHandler(InputElement.PointerPressedEvent, OnPathLinkPointerPressed, RoutingStrategies.Bubble);
    }

    private void OnCloseButtonClick(object sender, RoutedEventArgs e)
    {
        Close();
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
}
