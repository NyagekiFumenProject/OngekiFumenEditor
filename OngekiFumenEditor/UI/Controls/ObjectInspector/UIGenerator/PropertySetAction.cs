using Gemini.Modules.UndoRedo;
using OngekiFumenEditor.Properties;
using OngekiFumenEditor.Utils;
using System;

namespace OngekiFumenEditor.UI.Controls.ObjectInspector.UIGenerator
{
	public class PropertySetAction<T> : IUndoableAction
	{
		private readonly string propName;
		private readonly Action<T> setterAction;
		private readonly T oldValue;
		private readonly T newValue;

		public string Name => Resources.ObjectPropertyChanged.Format($"物件属性({propName})变更");

		public PropertySetAction(string propName, Action<T> setterAction, T oldValue, T newValue)
		{
			this.propName = propName;
			this.setterAction = setterAction;
			this.oldValue = oldValue;
			this.newValue = newValue;
		}

		public PropertySetAction(string propName, IObjectPropertyAccessProxy propertyWrapperCore, T oldValue, T newValue)
			: this(propName, (val) => propertyWrapperCore.ProxyValue = val, oldValue, newValue)
		{

		}

		public void Execute()
		{
			setterAction(newValue);
		}

		public void Undo()
		{
			setterAction(oldValue);
		}
	}

	public class PropertySetAction : PropertySetAction<object>
	{
		public PropertySetAction(string propName, Action<object> setterAction, object oldValue, object newValue) : base(propName, setterAction, oldValue, newValue)
		{
		}

		public PropertySetAction(string propName, IObjectPropertyAccessProxy propertyWrapperCore, object oldValue, object newValue) : base(propName, propertyWrapperCore, oldValue, newValue)
		{
		}
	}
}
