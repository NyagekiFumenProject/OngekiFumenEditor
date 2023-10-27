namespace OngekiFumenEditor.Base.OngekiObjects.ConnectableObject
{
	public abstract class ConnectableObjectBase : OngekiMovableObjectBase
	{
		public abstract int RecordId { get; set; }

		public override string ToString() => $"{base.ToString()} RID[{RecordId}]";

		public override void Copy(OngekiObjectBase fromObj)
		{
			base.Copy(fromObj);

			if (fromObj is not ConnectableObjectBase from)
				return;

			RecordId = from.RecordId;
		}

		public ConnectableChildObjectBase NextObject { get; set; }
	}
}
