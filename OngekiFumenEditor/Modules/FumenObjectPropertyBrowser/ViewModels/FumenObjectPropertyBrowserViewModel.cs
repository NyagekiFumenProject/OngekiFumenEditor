using Gemini.Framework;
using Gemini.Framework.Services;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Modules.FumenObjectPropertyBrowser.UIGenerator;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Modules.FumenObjectPropertyBrowser.ViewModels
{
    [Export(typeof(IFumenObjectPropertyBrowser))]
    public class FumenObjectPropertyBrowserViewModel : Tool, IFumenObjectPropertyBrowser
    {
        public override PaneLocation PreferredLocation => PaneLocation.Right;

        private OngekiObjectBase ongekiObject;
        public OngekiObjectBase OngekiObject
        {
            get
            {
                return ongekiObject;
            }
            set
            {
                ongekiObject = value;
                NotifyOfPropertyChange(() => OngekiObject);
                OnObjectChanged();
            }
        }

        public ObservableCollection<PropertyInfoWrapper> PropertyInfoWrappers { get; } = new ObservableCollection<PropertyInfoWrapper>();

        private void OnObjectChanged()
        {
            PropertyInfoWrappers.Clear();
            var propertyWrappers = (OngekiObject?.GetType()
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                ?? Array.Empty<PropertyInfo>())?
                .Where(x => x.CanWrite && x.CanRead)
                .Select(x => new PropertyInfoWrapper()
                {
                    OwnerObject = OngekiObject,
                    PropertyInfo = x
                }).OrderBy(x => x.DisplayPropertyName).ToArray();

            foreach (var wrapper in propertyWrappers)
            {
                PropertyInfoWrappers.Add(wrapper);
            }
        }

        public FumenObjectPropertyBrowserViewModel()
        {
            DisplayName = "物件属性";
        }
    }
}
