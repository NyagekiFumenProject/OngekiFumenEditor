using Caliburn.Micro;
using OngekiFumenEditor.Base.Attributes;
using System;
using System.ComponentModel;
using System.Reflection;

namespace OngekiFumenEditor.UI.Controls.ObjectInspector.UIGenerator
{
	public class PropertyInfoWrapper : PropertyChangedBase, IObjectPropertyAccessProxy
	{
		public PropertyInfo PropertyInfo { get; private set; }
		private object ownerObject;

		public PropertyInfoWrapper(PropertyInfo propertyInfo, object owner)
		{
			PropertyInfo = propertyInfo;
			ownerObject = owner;

			if (ProxyValue is INotifyPropertyChanged np)
				np.PropertyChanged += Op_PropertyChanged;
			if (ownerObject is INotifyPropertyChanged onp)
				onp.PropertyChanged += Onp_PropertyChanged;
		}

		public virtual object ProxyValue
		{
			get
			{
#if DEBUG
				if (ownerObject is null)
					throw new ObjectDisposedException(nameof(PropertyInfoWrapper));
#endif
				return PropertyInfo.GetValue(ownerObject);
			}
			set
			{
#if DEBUG
				if (ownerObject is null)
					throw new ObjectDisposedException(nameof(PropertyInfoWrapper));
#endif
				var valType = value?.GetType() ?? default;
				if (PropertyInfo.PropertyType == valType || valType is null || valType.IsAssignableTo(PropertyInfo.PropertyType))
				{
					SetValueInternal(value);
				}
				else
				{
					var actualType = TypeDescriptor.GetConverter(PropertyInfo.PropertyType);
					var actualValue = actualType.ConvertFrom(value);
					SetValueInternal(actualValue);
				}

				NotifyOfPropertyChange(() => ProxyValue);
			}
		}

		private void SetValueInternal(object newValue)
		{
			var oldValue = ProxyValue;
			if (oldValue is INotifyPropertyChanged op)
				op.PropertyChanged -= Op_PropertyChanged;

			if (newValue is INotifyPropertyChanged np)
				np.PropertyChanged += Op_PropertyChanged;

			PropertyInfo.SetValue(ownerObject, newValue);
		}

		private void Op_PropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			NotifyOfPropertyChange(() => ProxyValue);
		}

		private void Onp_PropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName == PropertyInfo.Name)
			{
				NotifyOfPropertyChange(() => ProxyValue);
				NotifyOfPropertyChange(e.PropertyName);
			}
		}

		public string DisplayPropertyName => PropertyInfo.GetCustomAttribute<ObjectPropertyBrowserAlias>()?.Alias ?? PropertyInfo.Name;
		public string DisplayPropertyTipText => PropertyInfo.GetCustomAttribute<ObjectPropertyBrowserTipText>()?.TipText ?? string.Empty;

		public bool IsAllowSetNull => PropertyInfo.GetCustomAttribute<ObjectPropertyBrowserAllowSetNull>() is not null;

		public override string ToString() => $"DisplayName:{DisplayPropertyName} PropValue:{ProxyValue}";

		public void Clear()
		{
			if (ProxyValue is INotifyPropertyChanged np)
				np.PropertyChanged -= Op_PropertyChanged;
			if (ownerObject is INotifyPropertyChanged onp)
				onp.PropertyChanged -= Onp_PropertyChanged;
			/*
            ownerObject = null;
            PropertyInfo = null;
            */
		}
	}
}
