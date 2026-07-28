using CommunityToolkit.Mvvm.ComponentModel;
using Gekimini.Avalonia.Modules.Window.ViewModels;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects;

namespace OngekiFumenEditor.Avalonia.UI.Controls.ObjectInspector.ViewModels.Dialogs;

public partial class BulletPalleteSelectDialogViewModel : WindowViewModelBase
{
    [ObservableProperty]
    private BulletPallete selectedPallete;

    public IEnumerable<BulletPallete> BulletPalleteList { get; }

    public BulletPalleteSelectDialogViewModel(IEnumerable<BulletPallete> list, BulletPallete initSelectedPallete)
    {
        BulletPalleteList = list ?? [];
        SelectedPallete = initSelectedPallete;
    }

    public async void OnItemDoubleClick(BulletPallete bulletPallete)
    {
        SelectedPallete = bulletPallete;
        await TryCloseAsync(true);
    }

    public async void OnComfirmButtonClicked()
    {
        await TryCloseAsync(true);
    }

    public async void OnCancelButtonClicked()
    {
        await TryCloseAsync(false);
    }
}
