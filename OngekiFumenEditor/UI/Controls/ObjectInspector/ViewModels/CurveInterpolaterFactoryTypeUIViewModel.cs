using Caliburn.Micro;
using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Kernel.CurveInterpolater;
using OngekiFumenEditor.Kernel.CurveInterpolater.DefaultImpl.Factory;
using OngekiFumenEditor.UI.Controls.ObjectInspector.UIGenerator;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.UI.Controls.ObjectInspector.ViewModels
{
    public class CurveInterpolaterFactoryTypeUIViewModel : CommonUIViewModelBase<ICurveInterpolaterFactory>
    {
        public IEnumerable<ICurveInterpolaterFactory> EnumValues => IoC.GetAll<ICurveInterpolaterFactory>();

        public ICurveInterpolaterFactory ProxyValue
        {
            get
            {
                var name = (PropertyInfo.ProxyValue as ICurveInterpolaterFactory).Name;
                return EnumValues.FirstOrDefault(x => x.Name == name);
            }
            set => PropertyInfo.ProxyValue = value;
        }

        public CurveInterpolaterFactoryTypeUIViewModel(IObjectPropertyAccessProxy wrapper) : base(wrapper)
        {

        }
    }
}
