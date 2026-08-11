#nullable enable

using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Gekimini.Avalonia.Modules.Window.Views;
using OngekiFumenEditor.Avalonia.Browser.Modules.BrowserOpfsBrowser.ViewModels;

namespace OngekiFumenEditor.Avalonia.Browser.Modules.BrowserOpfsBrowser.Views;

public partial class BrowserOpfsBrowserView : WindowViewBase
{
    private const double FolderTreeHideThreshold = 720;
    private bool closeApproved;
    private bool closeCheckRunning;

    public BrowserOpfsBrowserView()
    {
        InitializeComponent();
        Opened += OnOpened;
    }

    private async void OnOpened(object? sender, EventArgs e)
    {
        UpdateResponsiveLayout(Bounds.Width);
        if (DataContext is BrowserOpfsBrowserViewModel viewModel)
            await viewModel.OnWindowOpenedAsync();
    }

    protected override void OnClosed(EventArgs e)
    {
        if (DataContext is BrowserOpfsBrowserViewModel viewModel)
            viewModel.OnWindowClosed();
        base.OnClosed(e);
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        if (!closeApproved && DataContext is BrowserOpfsBrowserViewModel { IsDownloadInProgress: true } viewModel)
        {
            e.Cancel = true;
            if (!closeCheckRunning)
                _ = ConfirmCloseAsync(viewModel);
        }
        base.OnClosing(e);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == BoundsProperty)
            UpdateResponsiveLayout(Bounds.Width);
    }

    private async Task ConfirmCloseAsync(BrowserOpfsBrowserViewModel viewModel)
    {
        closeCheckRunning = true;
        try
        {
            if (!await viewModel.RequestCloseAsync())
                return;
            closeApproved = true;
            await CloseAsync(false);
        }
        finally
        {
            closeCheckRunning = false;
        }
    }

    private void UpdateResponsiveLayout(double width)
    {
        if (BrowserGrid is null || FolderTreePanel is null || FolderTreeSplitter is null ||
            BrowserGrid.ColumnDefinitions.Count < 2)
            return;

        bool showTree = width >= FolderTreeHideThreshold;
        FolderTreePanel.IsVisible = showTree;
        FolderTreeSplitter.IsVisible = showTree;
        BrowserGrid.ColumnDefinitions[0].Width = showTree ? new GridLength(230) : new GridLength(0);
        BrowserGrid.ColumnDefinitions[1].Width = showTree ? new GridLength(5) : new GridLength(0);
    }

    private void OnEntryGridKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.A || !e.KeyModifiers.HasFlag(KeyModifiers.Control) ||
            DataContext is not BrowserOpfsBrowserViewModel viewModel)
            return;

        viewModel.SelectAllCurrentEntries();
        e.Handled = true;
    }

    private void OnSortNameClick(object? sender, RoutedEventArgs e) =>
        SetSort(BrowserOpfsSortColumn.Name);

    private void OnSortTypeClick(object? sender, RoutedEventArgs e) =>
        SetSort(BrowserOpfsSortColumn.Type);

    private void OnSortSizeClick(object? sender, RoutedEventArgs e) =>
        SetSort(BrowserOpfsSortColumn.Size);

    private void OnSortModifiedTimeClick(object? sender, RoutedEventArgs e) =>
        SetSort(BrowserOpfsSortColumn.ModifiedTime);

    private void SetSort(BrowserOpfsSortColumn column)
    {
        if (DataContext is BrowserOpfsBrowserViewModel viewModel)
            viewModel.SetSort(column);
    }
}
