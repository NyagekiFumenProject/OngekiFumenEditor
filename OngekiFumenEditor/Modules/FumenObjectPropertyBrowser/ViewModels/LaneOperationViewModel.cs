using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Base.OngekiObjects.Lane;
using OngekiFumenEditor.Base.OngekiObjects.Wall;

namespace OngekiFumenEditor.Modules.FumenObjectPropertyBrowser.ViewModels
{
	public class LaneOperationViewModel : ConnectableObjectOperationViewModel
	{
		public char LaneChar => ConnectableObject.IDShortName[1];
		public char LaneTypeChar => ConnectableObject.IDShortName[0];

		public LaneOperationViewModel(ConnectableObjectBase obj) : base(obj)
		{

		}

		public override ConnectableChildObjectBase GenerateChildObject(bool needNext)
		{
			return ConnectableObject?.ReferenceStartObject?.CreateChildObject();
		}
	}
}
