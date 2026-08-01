using OngekiFumenEditor.Avalonia.Assets.Languages;
using OngekiFumenEditor.Avalonia.UI.Controls.ObjectInspector.UIGenerator;
using CommunityToolkit.Mvvm.Input;
using OngekiFumenEditor.Avalonia.Utils;
using System.IO;

namespace OngekiFumenEditor.Avalonia.UI.Controls.ObjectInspector.ViewModels;

public partial class FileInfoTypeUIViewModel : CommonUIViewModelBase<FileInfo>
{
    public FileInfo File
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
        var filePath = await FileDialogHelper.OpenFileAsync(Lang.SelectSvgFile, [(".svg", "Svg鏂囦欢")]);
        File = string.IsNullOrWhiteSpace(filePath) ? null : new FileInfo(filePath);
    }
}

