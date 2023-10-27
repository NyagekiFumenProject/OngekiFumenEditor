using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.UI.Controls.ObjectInspector.UIGenerator;
using System.Collections.Generic;

namespace OngekiFumenEditor.UI.Controls.ObjectInspector.ViewModels
{
	public class ColorIdEnumTypeUIViewModel : CommonUIViewModelBase
	{
		public IEnumerable<ColorId> EnumValues => ColorIdConst.AllColors;

		public ColorIdEnumTypeUIViewModel(IObjectPropertyAccessProxy wrapper) : base(wrapper)
		{

		}
	}
}
