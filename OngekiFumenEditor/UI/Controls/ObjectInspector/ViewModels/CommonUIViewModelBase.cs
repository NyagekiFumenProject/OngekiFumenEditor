using Caliburn.Micro;
using OngekiFumenEditor.UI.Controls.ObjectInspector.UIGenerator;

namespace OngekiFumenEditor.UI.Controls.ObjectInspector.ViewModels
{
	public abstract class CommonUIViewModelBase : PropertyChangedBase
	{
		private IObjectPropertyAccessProxy propertyInfo;

		public CommonUIViewModelBase(IObjectPropertyAccessProxy wrapper)
		{
			PropertyInfo = wrapper;
			PropertyInfo.PropertyChanged += PropertyInfo_PropertyChanged;
		}

		protected virtual void PropertyInfo_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
		{
			if (e.PropertyName == propertyInfo.PropertyInfo.Name)
			{
				NotifyOfPropertyChange(e.PropertyName);
			}
		}

		public IObjectPropertyAccessProxy PropertyInfo
		{
			get
			{
				return propertyInfo;
			}
			set
			{
				propertyInfo = value;
				NotifyOfPropertyChange(() => PropertyInfo);
			}
		}
	}

	public abstract class CommonUIViewModelBase<T> : CommonUIViewModelBase where T : class
	{
		public T TypedProxyValue
		{
			get => ProxyValue as T;
			set => ProxyValue = value;
		}

		public object ProxyValue
		{
			get => PropertyInfo.ProxyValue;
			set => PropertyInfo.ProxyValue = value;
		}

		protected CommonUIViewModelBase(IObjectPropertyAccessProxy wrapper) : base(wrapper)
		{

		}

		protected override void PropertyInfo_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
		{
			switch (e.PropertyName)
			{
				case nameof(ProxyValue):
					Refresh();
					break;
				default:
					base.PropertyInfo_PropertyChanged(sender, e);
					break;
			}
		}
	}
}
