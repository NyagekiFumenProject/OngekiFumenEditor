using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Base.OngekiObjects.Wall;

namespace OngekiFumenEditor.Modules.FumenObjectPropertyBrowser.ViewModels
{
	public class WallOperationViewModel : ConnectableObjectOperationViewModel
	{
		public bool IsLeftWall => ConnectableObject.IDShortName[1] == 'L';

		public WallOperationViewModel(ConnectableObjectBase obj) : base(obj)
		{

		}

		public override ConnectableChildObjectBase GenerateChildObject(bool needNext)
		{
			return IsLeftWall ? new WallLeftNext() : new WallRightNext();
		}
	}
}
