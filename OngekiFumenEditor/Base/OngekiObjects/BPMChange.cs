using System;

namespace OngekiFumenEditor.Base.OngekiObjects
{
	public class BPMChange : OngekiTimelineObjectBase
	{
		private double bpm = 240;
		public double BPM
		{
			get { return bpm; }
			set
			{
				bpm = value;
				NotifyOfPropertyChange(() => BPM);
			}
		}

		public static string CommandName => "BPM";
		public override string IDShortName => CommandName;

		public override string ToString() => $"{base.ToString()} Bpm[{BPM}]";

		public GridOffset LengthConvertToOffset(double len)
		{
			var totalGrid = len * (TGrid.ResT * BPM) / 240000;

			var p = totalGrid / TGrid.ResT;
			var unit = (int)p;
			var grid = (int)Math.Round((p - unit) * TGrid.ResT);

			return new GridOffset(unit, grid);
		}

		public override void Copy(OngekiObjectBase fromObj)
		{
			base.Copy(fromObj);

			if (fromObj is not BPMChange fromBpm)
				return;

			BPM = fromBpm.BPM;
		}
	}
}
