using Avalonia.Controls;
using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.UI.Controls.ObjectInspector.ViewModels;
using OngekiFumenEditor.Avalonia.Utils;
using System.IO;

namespace OngekiFumenEditor.Avalonia.UI.Controls.ObjectInspector.UIGenerator.TypeImplement;

[RegisterTransient<ITypeUIGenerator>]
public class FileInfoTypeGenerator : ITypeUIGenerator
{
    public IEnumerable<Type> SupportTypes { get; } = [typeof(FileInfo)];

    public Control Generate(IObjectPropertyAccessProxy wrapper)
        => ViewHelper.CreateViewByViewModelType(() => new FileInfoTypeUIViewModel(wrapper));
}
