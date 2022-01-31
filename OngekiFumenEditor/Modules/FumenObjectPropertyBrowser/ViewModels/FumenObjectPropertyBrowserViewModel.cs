using Gemini.Framework;
using Gemini.Framework.Services;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Modules.FumenObjectPropertyBrowser.Attrbutes;
using OngekiFumenEditor.Modules.FumenObjectPropertyBrowser.UIGenerator;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
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
        private FumenVisualEditorViewModel referenceEditor;

        public ObservableCollection<PropertyInfoWrapper> PropertyInfoWrappers { get; } = new ObservableCollection<PropertyInfoWrapper>();

        public OngekiObjectBase OngekiObject => ongekiObject;

        private void OnObjectChanged()
        {
            PropertyInfoWrappers.Clear();
            var propertyWrappers = (ongekiObject?.GetType()
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                ?? Array.Empty<PropertyInfo>())?
                .Where(x => x.CanWrite && x.CanRead)
                .Select(x => new UndoablePropertyInfoWrapper(new PropertyInfoWrapper()
                {
                    OwnerObject = ongekiObject,
                    PropertyInfo = x
                }, referenceEditor)).OrderBy(x => x.DisplayPropertyName).ToArray();

            foreach (var wrapper in propertyWrappers)
            {
                PropertyInfoWrappers.Add(wrapper);
            }
        }

        public void SetCurrentOngekiObject(OngekiObjectBase ongekiObject, FumenVisualEditorViewModel referenceEditor)
        {
            this.ongekiObject = ongekiObject;
            this.referenceEditor = referenceEditor;

            OnObjectChanged();
        }

        public FumenObjectPropertyBrowserViewModel()
        {
            DisplayName = "物件属性";
        }
    }
}
