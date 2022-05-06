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
        public IEnumerable<ICurveInterpolaterFactory> EnumValues => IoC.GetAll<ICurveInterpolaterFactory>();

        public CurveInterpolaterFactoryTypeUIViewModel(PropertyInfoWrapper wrapper) : base(wrapper)
        {

        }
    }
}
