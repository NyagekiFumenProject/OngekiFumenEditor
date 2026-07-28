using OngekiFumenEditor.Avalonia.Base.OngekiObjects.Beam;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects.ConnectableObject;

namespace OngekiFumenEditor.Avalonia.Modules.FumenObjectPropertyBrowser.ViewModels
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

