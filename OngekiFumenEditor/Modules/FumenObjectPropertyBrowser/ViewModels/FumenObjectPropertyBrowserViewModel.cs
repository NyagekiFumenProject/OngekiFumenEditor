using Caliburn.Micro;
using Gemini.Framework;
using Gemini.Framework.Services;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.Attributes;
using OngekiFumenEditor.Modules.FumenObjectPropertyBrowser.UIGenerator;
using OngekiFumenEditor.Modules.FumenVisualEditor.Kernel;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using OngekiFumenEditor.Properties;
using OngekiFumenEditor.UI.Controls.ObjectInspector.UIGenerator;
using OngekiFumenEditor.Utils;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using System.Linq;
using System.Reflection;

namespace OngekiFumenEditor.Modules.FumenObjectPropertyBrowser.ViewModels
{
	[Export(typeof(IFumenObjectPropertyBrowser))]
	public class FumenObjectPropertyBrowserViewModel : Tool, IFumenObjectPropertyBrowser
	{
		public override PaneLocation PreferredLocation => PaneLocation.Right;

		private HashSet<ISelectableObject> selectedObjects = new();
		public IReadOnlySet<ISelectableObject> SelectedObjects => selectedObjects;

		private FumenVisualEditorViewModel referenceEditor;
		private HashSet<Type> supportTypes;

		public ObservableCollection<IObjectPropertyAccessProxy> PropertyInfoWrappers { get; } = new();
		public FumenVisualEditorViewModel Editor => referenceEditor;

		private void OnObjectChanged()
		{
			foreach (var wrapper in PropertyInfoWrappers)
				wrapper.Clear();
			PropertyInfoWrappers.Clear();

			if (SelectedObjects.Count == 0)
				return;

			var genericProperties = SelectedObjects
				.Select(x => x.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(x =>
				{
					var type = x.PropertyType;
					if (supportTypes.Contains(type))
						return true;
					if (type.IsEnum)
						return true;
					return false;
				}))
				.IntersectManyBy(x => (x.PropertyType, x.Name))
				.Select(x => (x.PropertyType, x.Name, x))
				.ToArray();

			var wrappers = new List<IObjectPropertyAccessProxy>();
			foreach ((var propType, var propName, var refPropInfo) in genericProperties)
			{
				var wrapper = default(IObjectPropertyAccessProxy);
				if (SelectedObjects.Count > 1)
				{
					if (MultiObjectsPropertyInfoWrapper.TryCreate(propName, propType, selectedObjects, out var w))
						wrapper = new UndoableMultiObjectPropertyInfoWrapper(w, referenceEditor);
				}
				else
				{
					if (!refPropInfo.CanWrite)
					{
						if (refPropInfo.GetCustomAttribute<ObjectPropertyBrowserShow>() == null)
							continue;
					}
					if (refPropInfo.GetCustomAttribute<ObjectPropertyBrowserHide>() != null)
						continue;
					wrapper = new UndoablePropertyInfoWrapper(new PropertyInfoWrapper(refPropInfo, SelectedObjects.FirstOrDefault()), referenceEditor);
				}

				if (wrapper != null)
				{
					wrappers.Add(wrapper);
				}
			}

			foreach (var wrapper in wrappers.OrderBy(x => x.DisplayPropertyName))
				PropertyInfoWrappers.Add(wrapper);

			UpdateDisplayName();
		}

		private void UpdateDisplayName()
		{
			var singleObj = selectedObjects.Count == 1 ? selectedObjects.First() : null;
			DisplayName = Resources.ObjectProperty + (singleObj is null ? string.Empty : $" - {((OngekiObjectBase)singleObj).Name}");
		}

		public void RefreshSelected(IEnumerable<ISelectableObject> objects, FumenVisualEditorViewModel referenceEditor)
		{
			selectedObjects.Clear();
			selectedObjects.AddRange(objects);
			this.referenceEditor = referenceEditor;

			OnObjectChanged();
			referenceEditor?.NotifyOfPropertyChange(nameof(FumenVisualEditorViewModel.SelectObjects));
			//todo 解耦
			NotifyOfPropertyChange(nameof(SelectedObjects));
			UpdateDisplayName();
		}

		public void RefreshSelected(FumenVisualEditorViewModel referenceEditor, params object[] ongekiObj)
			=> RefreshSelected(ongekiObj.OfType<ISelectableObject>().FilterNull(), referenceEditor);

		public void RefreshSelected(FumenVisualEditorViewModel referenceEditor)
			=> RefreshSelected(referenceEditor?.SelectObjects ?? Enumerable.Empty<ISelectableObject>(), referenceEditor);

		public FumenObjectPropertyBrowserViewModel()
		{
			UpdateDisplayName();
			supportTypes = IoC.GetAll<ITypeUIGenerator>().SelectMany(x => x.SupportTypes).ToHashSet();

			IoC.Get<IEditorDocumentManager>().OnNotifyDestoryed += OnEditorDestoryed;
		}

		private void OnEditorDestoryed(FumenVisualEditorViewModel sender)
		{
			if (sender == referenceEditor)
				RefreshSelected(null);
		}
	}
}
