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
			switch (LaneTypeChar)
			{
				case 'W':
					return LaneChar switch
					{
						'L' => new WallLeftNext(),
						'R' => new WallRightNext(),
						_ => default
					};
				case 'C':
					return new ColorfulLaneNext();
				case 'E':
					return new EnemyLaneNext();
				case 'L':
					return LaneChar switch
					{
						'L' => new LaneLeftNext(),
						'C' => new LaneCenterNext(),
						'R' => new LaneRightNext(),
						_ => default
					};
				default:
					return default;
			}
		}
	}
}
