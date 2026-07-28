using OngekiFumenEditor.Avalonia.Base.OngekiObjects;
using OngekiFumenEditor.Avalonia.UI.Controls.ObjectInspector.UIGenerator;

namespace OngekiFumenEditor.Avalonia.UI.Controls.ObjectInspector.ViewModels;

public class ColorIdEnumTypeUIViewModel : CommonUIViewModelBase
{
    public IEnumerable<ColorId> EnumValues => ColorIdConst.AllColors;

    public ColorIdEnumTypeUIViewModel(IObjectPropertyAccessProxy wrapper) : base(wrapper)
    {
    }
}
