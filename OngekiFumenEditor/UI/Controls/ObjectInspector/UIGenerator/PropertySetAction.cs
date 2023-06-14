using Gemini.Modules.UndoRedo;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.UI.Controls.ObjectInspector.UIGenerator
{
    public class PropertySetAction<T> : IUndoableAction
    {
        private readonly string propName;
        private readonly Action<T> setterAction;
        private readonly T oldValue;
        private readonly T newValue;

        public string Name => $"物件属性({propName})变更";

        public PropertySetAction(string propName, Action<T> setterAction, T oldValue, T newValue)
        {
            this.propName = propName;
            this.setterAction = setterAction;
            this.oldValue = oldValue;
            this.newValue = newValue;
        }

        public PropertySetAction(string propName, PropertyInfoWrapper propertyWrapperCore, T oldValue, T newValue)
        {
            this.propName = propName;
            setterAction = (val) => propertyWrapperCore.ProxyValue = val;
            this.oldValue = oldValue;
            this.newValue = newValue;
        }

        public void Execute()
        {
            setterAction(newValue);
        }

        public void Undo()
        {
            setterAction(oldValue);
        }
    }

    public class PropertySetAction : PropertySetAction<object>
    {
        public PropertySetAction(string propName, Action<object> setterAction, object oldValue, object newValue) : base(propName, setterAction, oldValue, newValue)
        {
        }

        public PropertySetAction(string propName, PropertyInfoWrapper propertyWrapperCore, object oldValue, object newValue) : base(propName, propertyWrapperCore, oldValue, newValue)
        {
        }
    }
}
