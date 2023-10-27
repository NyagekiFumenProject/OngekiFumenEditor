using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;

namespace OngekiFumenEditor.Base.EditorObjects.LaneCurve
{
	public class LaneCurvePathControlObject : OngekiMovableObjectBase
	{
		public int Index { get; set; } = -1;

		private ConnectableChildObjectBase refCurveObject;
		public ConnectableChildObjectBase RefCurveObject
		{
			get => refCurveObject;
			set => Set(ref refCurveObject, value);
		}

		public const string CommandName = "[LCO_CTRL]";

		public override string IDShortName => CommandName;

		public override void Copy(OngekiObjectBase fromObj)
		{
			base.Copy(fromObj);

			if (fromObj is not LaneCurvePathControlObject from)
				return;

			Index = from.Index;
		}
	}
}
