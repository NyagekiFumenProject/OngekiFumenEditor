using OngekiFumenEditor.Core.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Core.Base.OngekiObjects.Lane.Base;
using OngekiFumenEditor.Core.Base.OngekiObjects.Lane;
using OngekiFumenEditor.Core.Base.OngekiObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Core.Base.EditorObjects
{
	public class AutoplayFaderLaneNext : LaneNextBase
	{
		public override string IDShortName => IsEndObject ? "[APFE]" : "[APFN]";
	}
}
