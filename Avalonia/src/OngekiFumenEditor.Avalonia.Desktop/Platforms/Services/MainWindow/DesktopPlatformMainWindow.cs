using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using CommunityToolkit.Mvvm.ComponentModel;
using Gekimini.Avalonia.Platforms.Services.MainWindow;
using Injectio.Attributes;

namespace OngekiFumenEditor.Avalonia.Desktop.Platforms.Services.MainWindow;

[RegisterSingleton<IPlatformMainWindow>]
public class DesktopPlatformMainWindow : ObservableObject, IPlatformMainWindow
{
    public bool IsFullScreen
    {
        get => GetCurrentMainWindow()?.WindowState == WindowState.FullScreen;
        set
        {
            if (GetCurrentMainWindow() is { } mainWindow)
            {
                if (value)
                    mainWindow.WindowState = WindowState.FullScreen;
                else
                    mainWindow.WindowState = WindowState.Normal;
            }

            OnPropertyChanged();
        }
    }

    public WindowIcon Icon
    {
        get => GetCurrentMainWindow()?.Icon;
        set
        {
            ApplyIcon(value);
            OnPropertyChanged();
        }
    }

    public string Title
    {
        get => GetCurrentMainWindow()?.Title;
        set
        {
            ApplyTitle(value);
            OnPropertyChanged();
        }
    }

    public Rect? WindowRect
    {
        get
        {
            if (GetCurrentMainWindow() is not { } mainWindow)
                return default;

            return new Rect(mainWindow.Position.X, mainWindow.Position.Y, mainWindow.Bounds.Width,
                mainWindow.Bounds.Height);
        }
        set
        {
            if (value is { } val)
                ApplyWindowRect(val);
            OnPropertyChanged();
        }
    }

    public Window GetCurrentMainWindow()
    {
        return (Application.Current.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;
    }

    private void ApplyWindowState(WindowState value)
    {
        if (GetCurrentMainWindow() is not { } mainWindow)
            return;

        mainWindow.WindowState = value;
    }

    private void ApplyTitle(string value)
    {
        if (GetCurrentMainWindow() is not { } mainWindow)
            return;

        mainWindow.Title = value;
    }

    private void ApplyWindowRect(Rect value)
    {
        if (GetCurrentMainWindow() is not { } mainWindow)
            return;

        mainWindow.Position = new PixelPoint((int) value.Position.X, (int) value.Position.Y);
        mainWindow.Width = value.Size.Width;
        mainWindow.Height = value.Size.Height;
    }

    private void ApplyIcon(WindowIcon value)
    {
        if (GetCurrentMainWindow() is not { } mainWindow)
            return;

        mainWindow.Icon = value;
    }
}