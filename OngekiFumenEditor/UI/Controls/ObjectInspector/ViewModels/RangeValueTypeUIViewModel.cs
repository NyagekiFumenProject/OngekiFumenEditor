using OngekiFumenEditor.Modules.FumenObjectPropertyBrowser.UIGenerator;
using OngekiFumenEditor.Core.Base;
using OngekiFumenEditor.UI.Controls.ObjectInspector.UIGenerator;

namespace OngekiFumenEditor.UI.Controls.ObjectInspector.ViewModels
{
	public class RangeValueTypeUIViewModel : CommonUIViewModelBase<RangeValue>
	{
		public double CurrentValue
		{
			get => TypedProxyValue.CurrentValue;
			set
			{
				if (PropertyInfo is UndoablePropertyInfoWrapper undoable)
					undoable.ExecuteSubPropertySetAction<float>(nameof(RangeValue.CurrentValue), (float val) => TypedProxyValue.CurrentValue = val, (float)TypedProxyValue.CurrentValue, (float)value);
				else
					TypedProxyValue.CurrentValue = (float)value;

				NotifyOfPropertyChange(() => CurrentValue);
			}
		}

		public double MinValue
		{
			get => TypedProxyValue.MinValue;
			set
			{
				if (PropertyInfo is UndoablePropertyInfoWrapper undoable)
					undoable.ExecuteSubPropertySetAction<float>(nameof(RangeValue.MinValue), (float val) => TypedProxyValue.MinValue = val, (float)TypedProxyValue.MinValue, (float)value);
				else
					TypedProxyValue.MinValue = (float)value;

				NotifyOfPropertyChange(() => MinValue);
			}
		}

		public double MaxValue
		{
			get => TypedProxyValue.MaxValue;
			set
			{
				if (PropertyInfo is UndoablePropertyInfoWrapper undoable)
					undoable.ExecuteSubPropertySetAction<float>(nameof(RangeValue.MaxValue), (float val) => TypedProxyValue.MaxValue = val, (float)TypedProxyValue.MaxValue, (float)value);
				else
					TypedProxyValue.MaxValue = (float)value;

				NotifyOfPropertyChange(() => MaxValue);
			}
		}

		public RangeValueTypeUIViewModel(IObjectPropertyAccessProxy wrapper) : base(wrapper)
		{

		}
	}
}
