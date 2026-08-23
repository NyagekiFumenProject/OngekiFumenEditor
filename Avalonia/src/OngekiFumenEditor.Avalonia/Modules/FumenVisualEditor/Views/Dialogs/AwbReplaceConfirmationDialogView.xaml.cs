using Avalonia.Controls;
using Avalonia.Interactivity;
using Gekimini.Avalonia.Modules.Window.Views;

namespace OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Views.Dialogs;

public partial class AwbReplaceConfirmationDialogView : WindowViewBase
{
    public AwbReplaceConfirmationDialogView()
    {
        InitializeComponent();
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        // Q9: the default focus must sit on the cancel button.
        CancelButton.Focus();
    }
}
