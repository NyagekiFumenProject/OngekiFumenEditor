using Gemini.Framework;
using Gemini.Framework.Services;
using OngekiFumenEditor.Base.Attributes;
using OngekiFumenEditor.UI.Controls.ObjectInspector.UIGenerator;
using OngekiFumenEditor.Utils;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;

namespace OngekiFumenEditor.UI.Controls.ObjectInspector.ViewModels
{
	public class ObjectInspectorViewModel : Tool
	{
		public override PaneLocation PreferredLocation => PaneLocation.Right;

		private object inspectObject;

		public ObservableCollection<IObjectPropertyAccessProxy> PropertyInfoWrappers { get; } = new();

		public object InspectObject
		{
			get => inspectObject;
			set
			{
				Set(ref inspectObject, value);
				OnObjectChanged();
			}
		}

		private void OnObjectChanged()
		{
			/*
            foreach (var wrapper in PropertyInfoWrappers)
                wrapper.Dispose();
            */
			PropertyInfoWrappers.Clear();

			var propertyWrappers = (inspectObject?.GetType()
				.GetProperties(BindingFlags.Public | BindingFlags.Instance) ?? Array.Empty<PropertyInfo>())
				.Where(x => x.CanRead)
				.Select(x => new PropertyInfoWrapper(x, inspectObject))
				.Select(x =>
				{
					if (x.PropertyInfo.GetCustomAttribute<ObjectPropertyBrowserHide>() is not null)
						return null;
					if (x.PropertyInfo.GetCustomAttribute<ObjectPropertyBrowserShow>() is not null)
						return x;
					return x;
				})
				.FilterNull()
				.OrderBy(x => x.DisplayPropertyName)
				.ToArray();

			foreach (var wrapper in propertyWrappers)
			{
				PropertyInfoWrappers.Add(wrapper);
			}
		}
	}
}
