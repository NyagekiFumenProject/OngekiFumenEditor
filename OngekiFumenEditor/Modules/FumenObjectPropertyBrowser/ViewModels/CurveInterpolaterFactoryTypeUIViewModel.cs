using Caliburn.Micro;
using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Kernel.CurveInterpolater;
using OngekiFumenEditor.Kernel.CurveInterpolater.DefaultImpl.Factory;
using OngekiFumenEditor.Modules.FumenObjectPropertyBrowser.UIGenerator;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Modules.FumenObjectPropertyBrowser.ViewModels
{
    public class CurveInterpolaterFactoryTypeUIViewModel : CommonUIViewModelBase<ICurveInterpolaterFactory>
    {
        public IEnumerable<string> EnumValues => IoC.GetAll<ICurveInterpolaterFactory>().Select(x => x.Name);

        public string FactoryName
        {
            get => TypedProxyValue?.Name;
            set
            {
                var def = DefaultCurveInterpolaterFactory.Default;
                if (PropertyInfo is UndoablePropertyInfoWrapper undoable)
                    undoable.ExecuteSubPropertySetAction(nameof(ConnectableChildObjectBase.CurveInterpolaterFactory), (val) =>
                    {
                        var factory = IoC.GetAll<ICurveInterpolaterFactory>().FirstOrDefault(x => x.Name == val) ?? def;
                        TypedProxyValue = factory;
                    }, FactoryName, value);
                else
                {
                    TypedProxyValue = IoC.GetAll<ICurveInterpolaterFactory>().FirstOrDefault(x => x.Name == value) ?? def;
                }

                NotifyOfPropertyChange(() => FactoryName);
            }
        }

        public CurveInterpolaterFactoryTypeUIViewModel(PropertyInfoWrapper wrapper) : base(wrapper)
        {
            if (PropertyInfo.OwnerObject is INotifyPropertyChanged notifyProperty)
            {
                notifyProperty.PropertyChanged += NotifyProperty_PropertyChanged;
            }
        }

        private void NotifyProperty_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ConnectableChildObjectBase.CurveInterpolaterFactory))
            {
                Refresh();
            }
        }
    }
}
