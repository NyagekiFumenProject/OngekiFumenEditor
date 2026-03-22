using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Base.OngekiObjects.Lane.Base;
using OngekiFumenEditor.Base.OngekiObjects.Lane;
using OngekiFumenEditor.Base.OngekiObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Base.EditorObjects
{
	public class AutoplayFaderLaneStart : LaneStartBase
	{
		public override string IDShortName => "[APFS]";

		public override LaneType LaneType => LaneType.AutoPlayFader;

		public override ConnectableChildObjectBase CreateChildObject() => new AutoplayFaderLaneNext();
	}
}
