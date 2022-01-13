using OngekiFumenEditor.Modules.FumenVisualEditor.Base;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Base
{
    public class EditorObjectDataTemplateSelector : DataTemplateSelector
    {
        private ResourceDictionary dataTemplates = new();
        public ResourceDictionary DataTemplates
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
                dateTemplate = DataTemplates.Values.OfType<DataTemplate>().FirstOrDefault(x =>
                dataType == (x.DataType as Type)
                ) ?? null;
                cachedDataTemplate[dataType] = dateTemplate;
            }

            return dateTemplate ?? base.SelectTemplate(item, container);
        }
    }
}
