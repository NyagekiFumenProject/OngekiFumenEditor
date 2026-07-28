using OngekiFumenEditor.Avalonia.Assets.Languages;
using OngekiFumenEditor.Avalonia.UI.Controls.ObjectInspector.UIGenerator;
using OngekiFumenEditor.Avalonia.Utils;
using System.IO;

namespace OngekiFumenEditor.Avalonia.UI.Controls.ObjectInspector.ViewModels;

public class FileInfoTypeUIViewModel : CommonUIViewModelBase<FileInfo>
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

    public async void OnSelectDialogOpen()
    {
        var filePath = await FileDialogHelper.OpenFileAsync(Lang.SelectSvgFile, [(".svg", "Svg鏂囦欢")]);
        File = string.IsNullOrWhiteSpace(filePath) ? null : new FileInfo(filePath);
    }
}

