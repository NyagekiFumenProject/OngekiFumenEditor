using OngekiFumenEditor.UI.Controls.ObjectInspector.UIGenerator;
using System;
using System.Collections.Generic;

namespace OngekiFumenEditor.UI.Controls.ObjectInspector.ViewModels
{
	public class EnumValueTypeUIViewModel : CommonUIViewModelBase
	{
		public IEnumerable<string> EnumValues => Enum.GetNames(PropertyInfo.PropertyInfo.PropertyType);

		public string Value
		{
			get => PropertyInfo.ProxyValue?.ToString();
			set => PropertyInfo.ProxyValue = Enum.Parse(PropertyInfo.PropertyInfo.PropertyType, value);
		}

		public EnumValueTypeUIViewModel(IObjectPropertyAccessProxy wrapper) : base(wrapper)
		{

		}
	}
}
