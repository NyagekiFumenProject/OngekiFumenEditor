using OngekiFumenEditor.Base.Attributes;
using OngekiFumenEditor.Base.OngekiObjects.Lane.Base;

namespace OngekiFumenEditor.Base.OngekiObjects
{
	public class Tap : OngekiMovableObjectBase, ILaneDockableChangable, ICriticalableObject
	{
		public bool IsWallTap => ReferenceLaneStart?.IsWallLane ?? false;

		private bool isCritical = false;
		public bool IsCritical
		{
			get { return isCritical; }
			set
			{
				isCritical = value;
				NotifyOfPropertyChange(() => IDShortName);
				NotifyOfPropertyChange(() => IsCritical);
			}
		}

		private LaneStartBase referenceLaneStart = default;
		public LaneStartBase ReferenceLaneStart
		{
			get { return referenceLaneStart; }
			set
			{
				referenceLaneStart = value;

				NotifyOfPropertyChange(() => ReferenceLaneStart);
				NotifyOfPropertyChange(() => ReferenceLaneStrId);
			}
		}

		[ObjectPropertyBrowserShow]
		[ObjectPropertyBrowserAlias("RefLaneId")]
		public int ReferenceLaneStrId => ReferenceLaneStart?.RecordId ?? -1;

		private int? referenceLaneStrIdManualSet = default;
		[ObjectPropertyBrowserShow]
		[ObjectPropertyBrowserTipText("ObjectLaneGroupId")]
		[ObjectPropertyBrowserAlias("SetRefLaneId")]
		public int? ReferenceLaneStrIdManualSet
		{
			get => referenceLaneStrIdManualSet;
			set
			{
				referenceLaneStrIdManualSet = value;
				NotifyOfPropertyChange(() => ReferenceLaneStrIdManualSet);
				referenceLaneStrIdManualSet = default;
			}
		}

		public override string IDShortName => this switch
		{
			{ IsCritical: true } => "CTP",
			{ IsCritical: false } => "TAP",
		};

		public override void Copy(OngekiObjectBase fromObj)
		{
			base.Copy(fromObj);

			if (fromObj is not Tap from)
				return;

			IsCritical = from.IsCritical;
			ReferenceLaneStart = from.ReferenceLaneStart;
		}
	}
}
