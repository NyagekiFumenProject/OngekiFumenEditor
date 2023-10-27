using OngekiFumenEditor.Modules.FumenObjectPropertyBrowser.UIGenerator;
using OngekiFumenEditor.UI.Controls.ObjectInspector.UIGenerator;

namespace OngekiFumenEditor.UI.Controls.ObjectInspector.ViewModels
{
	public class RangeValueTypeUIViewModel : CommonUIViewModelBase<Base.RangeValue>
	{
		public float CurrentValue
		{
			get => TypedProxyValue.CurrentValue;
			set
			{
				if (PropertyInfo is UndoablePropertyInfoWrapper undoable)
					undoable.ExecuteSubPropertySetAction(nameof(RangeValue.CurrentValue), (val) => TypedProxyValue.CurrentValue = val, CurrentValue, value);
				else
					TypedProxyValue.CurrentValue = value;

				NotifyOfPropertyChange(() => CurrentValue);
			}
		}

		public float MinValue
		{
			get => TypedProxyValue.MinValue;
			set
			{
				if (PropertyInfo is UndoablePropertyInfoWrapper undoable)
					undoable.ExecuteSubPropertySetAction(nameof(RangeValue.MinValue), (val) => TypedProxyValue.MinValue = val, MinValue, value);
				else
					TypedProxyValue.MinValue = value;

				NotifyOfPropertyChange(() => MinValue);
			}
		}

		public float MaxValue
		{
			get => TypedProxyValue.MaxValue;
			set
			{
				if (PropertyInfo is UndoablePropertyInfoWrapper undoable)
					undoable.ExecuteSubPropertySetAction(nameof(RangeValue.MaxValue), (val) => TypedProxyValue.MaxValue = val, MaxValue, value);
				else
					TypedProxyValue.MaxValue = value;

				NotifyOfPropertyChange(() => MaxValue);
			}
		}

		public RangeValueTypeUIViewModel(IObjectPropertyAccessProxy wrapper) : base(wrapper)
		{

		}
	}
}
