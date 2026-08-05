using OngekiFumenEditor.Avalonia.Assets.Languages;
using OngekiFumenEditor.Avalonia.UI.Controls.ObjectInspector.UIGenerator;
using CommunityToolkit.Mvvm.Input;
using OngekiFumenEditor.Avalonia.Utils;
using OngekiFumenEditor.Avalonia.Utils.SimpleFileSystem;

namespace OngekiFumenEditor.Avalonia.UI.Controls.ObjectInspector.ViewModels;

public partial class FileInfoTypeUIViewModel : CommonUIViewModelBase<ISimpleFile>
{
    public ISimpleFile File
    {
        get => TypedProxyValue;
        set
        {
            TypedProxyValue = value;
            OnPropertyChanged(nameof(File));
        }
    }

    public FileInfoTypeUIViewModel(IObjectPropertyAccessProxy wrapper) : base(wrapper)
    {
    }

    [RelayCommand]
    private async Task SelectFileAsync()
    {
        var selectedFile = await FileDialogHelper.OpenFileAsync(Lang.SelectSvgFile, [(".svg", "Svg鏂囦欢")]);
        if (selectedFile is null)
            return;

        File = selectedFile;
    }
}

