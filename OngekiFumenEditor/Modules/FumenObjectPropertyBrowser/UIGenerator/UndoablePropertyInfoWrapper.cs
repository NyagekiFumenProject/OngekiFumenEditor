using Caliburn.Micro;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using OngekiFumenEditor.UI.Controls.ObjectInspector.UIGenerator;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Modules.FumenObjectPropertyBrowser.UIGenerator
{
    public class UndoablePropertyInfoWrapper : PropertyChangedBase, IObjectPropertyAccessProxy
    {
        public PropertyInfo PropertyInfo => core.PropertyInfo;

        private IObjectPropertyAccessProxy core;
        private FumenVisualEditorViewModel referenceEditor;

        public UndoablePropertyInfoWrapper(IObjectPropertyAccessProxy propertyWrapperCore, FumenVisualEditorViewModel referenceEditor)
        {
            core = propertyWrapperCore;
            this.referenceEditor = referenceEditor;
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

        public void Dispose()
        {
            core.Dispose();
            core = null;
        }
    }
}
