using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Gekimini.Avalonia.Views;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.ViewModels;

namespace OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Views;

public partial class FumenVisualEditorView : ViewBase
{
    public FumenVisualEditorView()
    {
        InitializeComponent();
        AddHandler(DragDrop.DragEnterEvent, OnDragEnter);
        AddHandler(DragDrop.DragOverEvent, OnDragOver);
        AddHandler(DragDrop.DropEvent, OnDrop);
    }

    private void OnDragEnter(object sender, DragEventArgs e)
    {
        if (DataContext is FumenVisualEditorViewModel viewModel)
            viewModel.OnDragEnter(e);
    }

    private void OnDragOver(object sender, DragEventArgs e)
    {
        if (DataContext is FumenVisualEditorViewModel viewModel)
            viewModel.OnDragOver(e);
    }

    private void OnDrop(object sender, DragEventArgs e)
    {
        if (DataContext is FumenVisualEditorViewModel viewModel)
            viewModel.OnDrop(e, e.GetPosition(this));
    }

    private async void OnRenderControlHostLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is ContentControl contentControl &&
            DataContext is FumenVisualEditorViewModel { IsDisposed: false } viewModel)
            await viewModel.InitializeRenderControlAsync(contentControl);
    }
}
