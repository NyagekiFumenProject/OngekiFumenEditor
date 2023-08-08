using Caliburn.Micro;
using OngekiFumenEditor.Modules.FumenVisualEditor.Base;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using OngekiFumenEditor.UI.Controls.ObjectInspector.UIGenerator;
using OngekiFumenEditor.Utils;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Modules.FumenObjectPropertyBrowser.UIGenerator
{
    public class UndoableMultiObjectPropertyInfoWrapper : PropertyChangedBase, IObjectPropertyAccessProxy
    {
        public PropertyInfo PropertyInfo => core.PropertyInfo;

        private MultiObjectsPropertyInfoWrapper core;
        private FumenVisualEditorViewModel referenceEditor;

        public UndoableMultiObjectPropertyInfoWrapper(MultiObjectsPropertyInfoWrapper propertyWrapperCore, FumenVisualEditorViewModel referenceEditor)
        {
            core = propertyWrapperCore;
            this.referenceEditor = referenceEditor;
            core.PropertyChanged += Core_PropertyChanged;
        }

        private void Core_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(IObjectPropertyAccessProxy.ProxyValue):
                    NotifyOfPropertyChange(() => ProxyValue);
                    break;
                default:
                    NotifyOfPropertyChange(e.PropertyName);
                    break;
            }
        }

        public object ProxyValue
        {
            get => core.ProxyValue;
            set
            {
                var oldValues = core.Wrappers.Select(x => x.ProxyValue).ToArray();
                var newValue = value;

                referenceEditor.UndoRedoManager.ExecuteAction(LambdaUndoAction.Create($"批量赋值{core.PropertyInfo.Name}",
                    () =>
                    {
                        core.ProxyValue = newValue;
                    },
                    () =>
                    {
                        for (int i = 0; i < oldValues.Length; i++)
                            core.Wrappers[i].ProxyValue = oldValues[i];
                    }));

                NotifyOfPropertyChange(() => ProxyValue);
            }
        }

        public string DisplayPropertyName => core.DisplayPropertyName;
        public string DisplayPropertyTipText => core.DisplayPropertyTipText;

        public override string ToString() => $"[Undoable]{base.ToString()}";

        public void Clear()
        {
            core.PropertyChanged -= Core_PropertyChanged;
            core.Clear();
        }
    }
}
