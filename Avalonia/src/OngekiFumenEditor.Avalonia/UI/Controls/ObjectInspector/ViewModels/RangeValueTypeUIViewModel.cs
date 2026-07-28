using OngekiFumenEditor.Avalonia.UI.Controls.ObjectInspector.UIGenerator;

namespace OngekiFumenEditor.Avalonia.UI.Controls.ObjectInspector.ViewModels;

public class RangeValueTypeUIViewModel : CommonUIViewModelBase<Base.RangeValue>
{
    public float CurrentValue
    {
        get => TypedProxyValue.CurrentValue;
        set
        {
            TypedProxyValue.CurrentValue = value;
            OnPropertyChanged(nameof(CurrentValue));
        }
    }

    public float MinValue
    {
        get => TypedProxyValue.MinValue;
        set
        {
            TypedProxyValue.MinValue = value;
            OnPropertyChanged(nameof(MinValue));
        }
    }

    public float MaxValue
    {
        get => TypedProxyValue.MaxValue;
        set
        {
            TypedProxyValue.MaxValue = value;
            OnPropertyChanged(nameof(MaxValue));
        }
    }

    public RangeValueTypeUIViewModel(IObjectPropertyAccessProxy wrapper) : base(wrapper)
    {
    }
}
