using CommunityToolkit.Mvvm.ComponentModel;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Base.DropActions;

namespace OngekiFumenEditor.Avalonia.Modules.FumenObjectPropertyBrowser.ViewModels
{
	public class HoldOperationViewModel : ObservableObject
	{
		private Hold connectableObject;
		public Hold ConnectableObject
		{
			get
			{
				return connectableObject;
			}
			set
			{
				SetProperty(ref connectableObject, value);
				CheckEnableDrag();
			}
		}

		private bool isEnableDrag = true;
		public bool IsEnableDrag
		{
			get
			{
				return isEnableDrag;
			}
			set
			{
				SetProperty(ref isEnableDrag, value);
			}
		}

		private void CheckEnableDrag()
		{
			IsEnableDrag = !(ConnectableObject.HoldEnd is not null);
		}

		public HoldOperationViewModel(Hold obj)
		{
			ConnectableObject = obj;
		}

		public OngekiObjectDropParam CreateHoldEndDropAction()
		{
			return new OngekiObjectDropParam(() =>
			{
				var genWallChild = new HoldEnd();
				ConnectableObject.SetHoldEnd(genWallChild);
				CheckEnableDrag();
				return genWallChild;
			});
		}
	}
}

