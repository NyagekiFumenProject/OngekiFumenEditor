using Caliburn.Micro;
using Gemini.Modules.UndoRedo;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using OngekiFumenEditor.Modules.FumenVisualEditorSettings.ViewModels;
using OngekiFumenEditor.Modules.FumenVisualEditorSettings.Views;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Modules.FumenObjectPropertyBrowser.UIGenerator
{
    public class PropertyInfoWrapper : PropertyChangedBase
    {
        public PropertyInfo PropertyInfo { get; set; }
        public object OwnerObject { get; set; }

        public virtual object ProxyValue
        {
            get
            {
                return PropertyInfo.GetValue(OwnerObject);
            }
            set
            {
                if (PropertyInfo.PropertyType == (value?.GetType() ?? default))
                {
                    PropertyInfo.SetValue(OwnerObject, value);
                }
                else
                {
                    var actualType = TypeDescriptor.GetConverter(PropertyInfo.PropertyType);
                    PropertyInfo.SetValue(OwnerObject, actualType.ConvertFrom(value));
                }

                NotifyOfPropertyChange(() => ProxyValue);
            }
        }

        public string DisplayPropertyName => PropertyInfo.Name;

        public override string ToString() => $"PropName:{DisplayPropertyName} PropValue:{ProxyValue}";
    }

    public class PropertyInfoWrapper<T> : PropertyInfoWrapper
    {
        public new T ProxyValue
        {
            get
            {
                return (T)PropertyInfo.GetValue(OwnerObject);
            }
            set
            {
                PropertyInfo.SetValue(OwnerObject, value);
                NotifyOfPropertyChange(() => ProxyValue);
            }
        }
    }

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

        private class PropertySetAction : IUndoableAction
        {
            private readonly PropertyInfoWrapper propertyWrapperCore;
            private readonly object oldValue;
            private readonly object newValue;

            public string Name => "物件属性变更";

            public PropertySetAction(PropertyInfoWrapper propertyWrapperCore, object oldValue, object newValue)
            {
                this.propertyWrapperCore = propertyWrapperCore;
                this.oldValue = oldValue;
                this.newValue = newValue;
            }

            public void Execute()
            {
                propertyWrapperCore.ProxyValue = newValue;
            }

            public void Undo()
            {
                propertyWrapperCore.ProxyValue = oldValue;
            }
        }

        public override object ProxyValue
        {
            get => base.ProxyValue;
            set
            {
                var oldValue = ProxyValue;
                var newValue = value;

                referenceEditor.UndoRedoManager.ExecuteAction(new PropertySetAction(propertyWrapperCore, oldValue, newValue));
                NotifyOfPropertyChange(() => ProxyValue);
            }
        }

        public override string ToString() => $"[Undoable]{base.ToString()}";
    }
}
