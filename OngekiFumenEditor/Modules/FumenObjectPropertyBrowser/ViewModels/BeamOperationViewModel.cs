using OngekiFumenEditor.Base.OngekiObjects.Beam;
using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;

namespace OngekiFumenEditor.Modules.FumenObjectPropertyBrowser.ViewModels
{
	public class BeamOperationViewModel : ConnectableObjectOperationViewModel
	{
		public BeamOperationViewModel(ConnectableObjectBase obj) : base(obj)
		{

		}

		public override ConnectableChildObjectBase GenerateChildObject(bool needNext)
		{
			return new BeamNext();
		}
	}
}
