#nullable enable

using Avalonia.Controls;
using Avalonia.Layout;
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

    private void OnJacketImageEffectiveViewportChanged(object? sender, EffectiveViewportChangedEventArgs e)
    {
        if (sender is not Layoutable image ||
            e.EffectiveViewport.Width <= 0 ||
            e.EffectiveViewport.Height <= 0 ||
            !e.EffectiveViewport.Intersects(new global::Avalonia.Rect(image.Bounds.Size)))
            return;

        RequestJacketLoad(sender);
    }

    private void OnTrackRequestBringIntoView(object? sender, RequestBringIntoViewEventArgs e)
    {
        // The item header is an interaction surface, not a navigation target.
        // Suppress its automatic bring-into-view request so expanding an item
        // cannot move the list to a different position.
        if (sender is Expander)
            e.Handled = true;
    }

    private void RequestJacketLoad(object? sender)
    {
        if (sender is not Image image ||
            image.DataContext is not OngekiFumenSet set ||
            DataContext is not OgkiFumenListBrowserViewModel viewModel)
            return;

        viewModel.RequestJacketLoad(set);
    }
}
