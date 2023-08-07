using AngleSharp.Css;
using Caliburn.Micro;
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
using System.DirectoryServices.ActiveDirectory;
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

        private HashSet<ISelectableObject> selectedObjects = new();
        public IReadOnlySet<ISelectableObject> SelectedObjects => selectedObjects;

        private FumenVisualEditorViewModel referenceEditor;
        private HashSet<Type> supportTypes;

        public ObservableCollection<IObjectPropertyAccessProxy> PropertyInfoWrappers { get; } = new();
        public FumenVisualEditorViewModel Editor => referenceEditor;

        private void OnObjectChanged()
        {
            foreach (var wrapper in PropertyInfoWrappers)
                wrapper.Dispose();
            PropertyInfoWrappers.Clear();

            if (SelectedObjects.Count == 0)
                return;

            var genericProperties = SelectedObjects
                .Select(x => x.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(x => supportTypes.Contains(x.PropertyType)))
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
            DisplayName = "物件属性" + (singleObj is null ? string.Empty : $" - {((OngekiObjectBase)singleObj).Name}");
        }

        public void RefreshSelected(IEnumerable<ISelectableObject> objects, FumenVisualEditorViewModel referenceEditor)
        {
            selectedObjects.Clear();
            selectedObjects.AddRange(objects);
            this.referenceEditor = referenceEditor;

            OnObjectChanged();
            referenceEditor.NotifyOfPropertyChange(nameof(FumenVisualEditorViewModel.SelectObjects));
            NotifyOfPropertyChange(nameof(SelectedObjects));
            UpdateDisplayName();
        }

        public void RefreshSelected(FumenVisualEditorViewModel referenceEditor)
            => RefreshSelected(referenceEditor.SelectObjects, referenceEditor);

        public FumenObjectPropertyBrowserViewModel()
        {
            UpdateDisplayName();
            supportTypes = IoC.GetAll<ITypeUIGenerator>().SelectMany(x => x.SupportTypes).ToHashSet();
        }
    }
}
