using Caliburn.Micro;
using OngekiFumenEditor.Base.Attributes;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using OngekiFumenEditor.UI.Controls.ObjectInspector.UIGenerator;
using System;
using System.ComponentModel;
using System.Reflection;

namespace OngekiFumenEditor.Modules.FumenObjectPropertyBrowser.UIGenerator
{
	public class UndoablePropertyInfoWrapper : PropertyChangedBase, IObjectPropertyAccessProxy
	{
		public PropertyInfo PropertyInfo => core.PropertyInfo;

		private IObjectPropertyAccessProxy core;
		private FumenVisualEditorViewModel referenceEditor;

		public bool IsAllowSetNull => PropertyInfo.GetCustomAttribute<ObjectPropertyBrowserAllowSetNull>() is not null;

		public UndoablePropertyInfoWrapper(IObjectPropertyAccessProxy propertyWrapperCore, FumenVisualEditorViewModel referenceEditor)
		{
			core = propertyWrapperCore;
			this.referenceEditor = referenceEditor;
			core.PropertyChanged += Core_PropertyChanged;
		}

		private void Core_PropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			switch (e.PropertyName)
			{
				case nameof(IObjectPropertyAccessProxy.ProxyValue):
					NotifyOfPropertyChange(() => ProxyValue);
					break;
				default:
					NotifyOfPropertyChange(e.PropertyName);
					break;
			}
		}

		public object ProxyValue
		{
			get => core.ProxyValue;
			set
			{
				var oldValue = ProxyValue;
				var newValue = value;
				referenceEditor.UndoRedoManager.ExecuteAction(new PropertySetAction(core.PropertyInfo.Name, core, oldValue, newValue));

				NotifyOfPropertyChange(() => ProxyValue);
			}
		}

		public string DisplayPropertyName => core.DisplayPropertyName;
		public string DisplayPropertyTipText => core.DisplayPropertyTipText;

		public void ExecuteSubPropertySetAction<T>(string subPropName, Action<T> setterAction, T oldValue, T newValue)
		{
			referenceEditor.UndoRedoManager.ExecuteAction(new PropertySetAction<T>($"{PropertyInfo.Name}.{subPropName}", setterAction, oldValue, newValue));
			NotifyOfPropertyChange(() => ProxyValue);
		}

		public override string ToString() => $"[Undoable]{base.ToString()}";

		public void Clear()
		{
			core.PropertyChanged -= Core_PropertyChanged;
			core.Clear();
			//core = null;
		}
	}
}
