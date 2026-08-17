using Avalonia.Controls;
using Gekimini.Avalonia.Modules.Window.Views;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.ViewModels.Dialogs;

namespace OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Views.Dialogs;

public partial class EditorProjectSetupDialogView : WindowViewBase
{
    public EditorProjectSetupDialogView()
    {
        InitializeComponent();
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        if (DataContext is EditorProjectSetupDialogViewModel viewModel &&
            viewModel.HandleWindowClosing())
        {
            e.Cancel = true;
        }

        base.OnClosing(e);
    }
}
