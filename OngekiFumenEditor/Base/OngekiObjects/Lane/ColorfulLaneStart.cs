using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Base.OngekiObjects.Lane.Base;
using OngekiFumenEditor.Kernel.CurveInterpolater;
using System;
using System.Collections.Generic;

namespace OngekiFumenEditor.Base.OngekiObjects.Lane
{
	public class ColorfulLaneStart : LaneStartBase, IColorfulLane
	{
		public override string IDShortName => "CLS";

		public override LaneType LaneType => LaneType.Colorful;

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

		public override ConnectableChildObjectBase CreateChildObject() => new ColorfulLaneNext();

		public override void Copy(OngekiObjectBase fromObj)
		{
			base.Copy(fromObj);

			if (fromObj is not ColorfulLaneStart cls)
				return;

			ColorId = cls.ColorId;
			Brightness = cls.Brightness;
		}

		public override IEnumerable<ConnectableStartObject> InterpolateCurve(Func<ConnectableStartObject> genStartFunc, Func<ConnectableChildObjectBase> genNextFunc, ICurveInterpolaterFactory factory = null)
		{
			void Copy(OngekiObjectBase fromObj)
			{
				var obj = fromObj as IColorfulLane;
				obj.ColorId = ColorId;
				obj.Brightness = Brightness;
			}

			var overrideGenStartFunc = () =>
			{
				var obj = genStartFunc();
				Copy(obj);
				return obj;
			};
			var overrideGenNextFunc = () =>
			{
				var obj = genNextFunc();
				Copy(obj);
				return obj;
			};
			return base.InterpolateCurve(overrideGenStartFunc, overrideGenNextFunc, factory);
		}
	}
}
