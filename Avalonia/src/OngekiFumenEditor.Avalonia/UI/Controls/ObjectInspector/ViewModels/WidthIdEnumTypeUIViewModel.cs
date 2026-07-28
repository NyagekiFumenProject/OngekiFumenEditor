using OngekiFumenEditor.Avalonia.Base.OngekiObjects;
using OngekiFumenEditor.Avalonia.UI.Controls.ObjectInspector.UIGenerator;

namespace OngekiFumenEditor.Avalonia.UI.Controls.ObjectInspector.ViewModels;

public class WidthIdEnumTypeUIViewModel : CommonUIViewModelBase
{
    public IEnumerable<WidthId> EnumValues => WidthIdConst.AllWidthIds;

    public WidthIdEnumTypeUIViewModel(IObjectPropertyAccessProxy wrapper) : base(wrapper)
    {
    }
}
