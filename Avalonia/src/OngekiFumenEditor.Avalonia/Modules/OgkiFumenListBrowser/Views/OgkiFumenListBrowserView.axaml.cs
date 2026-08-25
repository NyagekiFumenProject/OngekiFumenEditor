#nullable enable

using Avalonia.Controls;
using Avalonia.VisualTree;
using Gekimini.Avalonia.Modules.Window.Views;
using OngekiFumenEditor.Avalonia.Modules.OgkiFumenListBrowser.Models;
using OngekiFumenEditor.Avalonia.Modules.OgkiFumenListBrowser.ViewModels;

namespace OngekiFumenEditor.Avalonia.Modules.OgkiFumenListBrowser.Views;

public partial class OgkiFumenListBrowserView : WindowViewBase
{
    public OgkiFumenListBrowserView()
    {
        InitializeComponent();
    }

    private void OnJacketImageAttached(object? sender, VisualTreeAttachmentEventArgs e) =>
        RequestJacketLoad(sender);

    private void OnJacketImageDataContextChanged(object? sender, EventArgs e) =>
        RequestJacketLoad(sender);

    private void RequestJacketLoad(object? sender)
    {
        if (sender is not Image image ||
            image.DataContext is not OngekiFumenSet set ||
            DataContext is not OgkiFumenListBrowserViewModel viewModel)
            return;

        viewModel.RequestJacketLoad(set);
    }
}
