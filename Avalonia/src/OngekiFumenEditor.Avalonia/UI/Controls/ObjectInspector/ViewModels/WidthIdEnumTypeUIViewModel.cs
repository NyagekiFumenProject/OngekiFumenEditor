using OngekiFumenEditor.Avalonia.Base.OngekiObjects;
using OngekiFumenEditor.Avalonia.UI.Controls.ObjectInspector.UIGenerator;

namespace OngekiFumenEditor.Avalonia.UI.Controls.ObjectInspector.ViewModels;

public class WidthIdEnumTypeUIViewModel : CommonUIViewModelBase<WidthId>
{
    public IEnumerable<WidthId> EnumValues => WidthIdConst.AllWidthIds;

    public WidthId Value
    {
        get => TypedProxyValue;
        set => TypedProxyValue = value;
    }

    public WidthIdEnumTypeUIViewModel(IObjectPropertyAccessProxy wrapper) : base(wrapper)
    {
    }

    protected override void Refresh()
    {
        OnPropertyChanged(nameof(Value));
    }
}
