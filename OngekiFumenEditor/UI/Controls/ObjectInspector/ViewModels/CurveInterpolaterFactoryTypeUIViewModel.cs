using Caliburn.Micro;
using OngekiFumenEditor.Kernel.CurveInterpolater;
using OngekiFumenEditor.UI.Controls.ObjectInspector.UIGenerator;
using System.Collections.Generic;
using System.Linq;

namespace OngekiFumenEditor.UI.Controls.ObjectInspector.ViewModels
{
	public class CurveInterpolaterFactoryTypeUIViewModel : CommonUIViewModelBase
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
