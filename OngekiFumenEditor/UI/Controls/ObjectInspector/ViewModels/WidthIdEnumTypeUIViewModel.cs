using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.UI.Controls.ObjectInspector.UIGenerator;
using System.Collections.Generic;

namespace OngekiFumenEditor.UI.Controls.ObjectInspector.ViewModels
{
	public class WidthIdEnumTypeUIViewModel : CommonUIViewModelBase
	{
		public IEnumerable<WidthId> EnumValues => WidthIdConst.AllWidthIds;

		public WidthIdEnumTypeUIViewModel(IObjectPropertyAccessProxy wrapper) : base(wrapper)
		{

		}
	}
}
