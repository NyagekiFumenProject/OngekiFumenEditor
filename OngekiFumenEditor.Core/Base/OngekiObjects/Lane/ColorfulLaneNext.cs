using OngekiFumenEditor.Base.OngekiObjects.Lane.Base;

namespace OngekiFumenEditor.Base.OngekiObjects.Lane
{
	public class ColorfulLaneNext : LaneNextBase, IColorfulLane
	{
		public override string IDShortName => IsEndObject ? "CLE" : "CLN";

		private ColorId colorId = ColorIdConst.Akari;
		public ColorId ColorId
		{
			get => colorId;
			set => Set(ref colorId, value);
		}

		private int brightness = 3;
		public int Brightness
		{
			get => brightness;
			set => Set(ref brightness, value);
		}

		public override void Copy(OngekiObjectBase fromObj)
		{
			base.Copy(fromObj);

			if (fromObj is not ColorfulLaneStart cls)
				return;

			ColorId = cls.ColorId;
			Brightness = cls.Brightness;
		}
	}
}
