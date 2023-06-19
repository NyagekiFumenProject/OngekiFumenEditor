using Gemini.Framework;
using Gemini.Framework.Services;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.Attributes;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Modules.FumenObjectPropertyBrowser.UIGenerator;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using OngekiFumenEditor.UI.Controls.ObjectInspector.UIGenerator;
using OngekiFumenEditor.Utils;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.UI.Controls.ObjectInspector.ViewModels
{
    public class ObjectInspectorViewViewModel : Tool
    {
        public override PaneLocation PreferredLocation => PaneLocation.Right;

        private object inspectObject;

        public ObservableCollection<PropertyInfoWrapper> PropertyInfoWrappers { get; } = new ObservableCollection<PropertyInfoWrapper>();

        private void OnObjectChanged()
        {
            PropertyInfoWrappers.Clear();
            var propertyWrappers = (inspectObject?.GetType()
                .GetProperties(BindingFlags.Public | BindingFlags.Instance) ?? Array.Empty<PropertyInfo>())
                .Where(x => x.CanRead)
                .Select(x => new PropertyInfoWrapper()
                {
                    OwnerObject = inspectObject,
                    PropertyInfo = x
                })
                .Select(x =>
                {
                    if (x.PropertyInfo.GetCustomAttribute<ObjectPropertyBrowserHide>() is not null)
                        return null;
                    if (x.PropertyInfo.GetCustomAttribute<ObjectPropertyBrowserShow>() is not null)
                        return x;
                    return null;
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
