using OngekiFumenEditor.Avalonia.Base.OngekiObjects;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects.Lane.Base;

namespace OngekiFumenEditor.Avalonia.Base.EditorObjects
{
	public class AutoplayFaderLaneStart : LaneStartBase
	{
		public override string IDShortName => "[APFS]";

		public override LaneType LaneType => LaneType.AutoPlayFader;

		public override ConnectableChildObjectBase CreateChildObject() => new AutoplayFaderLaneNext();
	}
}
