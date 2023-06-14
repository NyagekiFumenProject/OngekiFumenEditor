using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using OngekiFumenEditor.UI.Controls.ObjectInspector.UIGenerator;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Modules.FumenObjectPropertyBrowser.UIGenerator
{
    public class UndoablePropertyInfoWrapper : PropertyInfoWrapper
    {
        private PropertyInfoWrapper propertyWrapperCore;
        private FumenVisualEditorViewModel referenceEditor;

        public UndoablePropertyInfoWrapper(PropertyInfoWrapper propertyWrapperCore, FumenVisualEditorViewModel referenceEditor)
        {
            this.propertyWrapperCore = propertyWrapperCore;
            PropertyInfo = propertyWrapperCore.PropertyInfo;
            OwnerObject = propertyWrapperCore.OwnerObject;
            this.referenceEditor = referenceEditor;
        }

        public override object ProxyValue
        {
            get => base.ProxyValue;
            set
            {
                var oldValue = ProxyValue;
                var newValue = value;
                referenceEditor.UndoRedoManager.ExecuteAction(new PropertySetAction(PropertyInfo.Name, propertyWrapperCore, oldValue, newValue));

                NotifyOfPropertyChange(() => ProxyValue);
            }
        }

        public void ExecuteSubPropertySetAction<T>(string subPropName, Action<T> setterAction, T oldValue, T newValue)
        {
            referenceEditor.UndoRedoManager.ExecuteAction(new PropertySetAction<T>($"{PropertyInfo.Name}.{subPropName}", setterAction, oldValue, newValue));
            NotifyOfPropertyChange(() => ProxyValue);
        }

        public override string ToString() => $"[Undoable]{base.ToString()}";
    }
}
