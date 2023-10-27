using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Base
{
	public class EditorObjectDataTemplateSelector : DataTemplateSelector
	{
		private List<DataTemplate> dataTemplates = new();
		public List<DataTemplate> DataTemplates
		{
			get => dataTemplates;
			set
			{
				dataTemplates = value;
				cachedDataTemplate.Clear();
			}
		}

		private Dictionary<Type, DataTemplate> cachedDataTemplate = new();

		public override DataTemplate SelectTemplate(object item, DependencyObject container)
		{
			var dataType = item.GetType();

			if (!cachedDataTemplate.TryGetValue(dataType, out var dateTemplate))
			{
				dateTemplate = DataTemplates.FirstOrDefault(x => dataType == (x.DataType as Type)) ?? null;
				cachedDataTemplate[dataType] = dateTemplate;
			}

			return dateTemplate ?? base.SelectTemplate(item, container);
		}
	}
}
