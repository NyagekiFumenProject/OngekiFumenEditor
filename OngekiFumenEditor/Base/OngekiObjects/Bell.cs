using OngekiFumenEditor.Base.Attributes;
using OngekiFumenEditor.Base.OngekiObjects.BulletPalleteEnums;
using System.Collections.Generic;
using static OngekiFumenEditor.Base.OngekiObjects.BulletPallete;

namespace OngekiFumenEditor.Base.OngekiObjects
{
	public class Bell : OngekiMovableObjectBase, IBulletPalleteReferencable
	{
		public static string CommandName => "BEL";
		public override string IDShortName => CommandName;

		public Bell()
		{
			ReferenceBulletPallete = null;
		}

		private BulletPallete referenceBulletPallete;
		public BulletPallete ReferenceBulletPallete
		{
			get { return referenceBulletPallete; }
			set
			{
				if (value is not null)
					value.PropertyChanged -= ReferenceBulletPallete_PropertyChanged;
				referenceBulletPallete = value;
				if (value is not null)
					value.PropertyChanged += ReferenceBulletPallete_PropertyChanged;
				NotifyOfPropertyChange(() => ReferenceBulletPallete);

				NotifyOfPropertyChange(() => Speed);
				NotifyOfPropertyChange(() => StrID);
				NotifyOfPropertyChange(() => PlaceOffset);
				NotifyOfPropertyChange(() => TypeValue);
				NotifyOfPropertyChange(() => TargetValue);
				NotifyOfPropertyChange(() => ShooterValue);
                NotifyOfPropertyChange(() => RandomOffsetRange);
                NotifyOfPropertyChange(() => SizeValue);
			}
		}

		private void ReferenceBulletPallete_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
		{
			switch (e.PropertyName)
			{
				case nameof(BulletPallete.StrID):
				case nameof(BulletPallete.PlaceOffset):
				case nameof(BulletPallete.TypeValue):
				case nameof(BulletPallete.TargetValue):
				case nameof(BulletPallete.ShooterValue):
				case nameof(BulletPallete.SizeValue):
				case nameof(BulletPallete.Speed):
                case nameof(BulletPallete.RandomOffsetRange):
                    NotifyOfPropertyChange(e.PropertyName);
					break;
			}
		}


		[ObjectPropertyBrowserAlias("BPL." + nameof(Speed))]
		[ObjectPropertyBrowserShow]
		public float Speed => ReferenceBulletPallete?.Speed ?? default;

        [ObjectPropertyBrowserAlias("BPL." + nameof(RandomOffsetRange))]
        [ObjectPropertyBrowserShow]
        public float RandomOffsetRange => ReferenceBulletPallete?.RandomOffsetRange ?? default;

        [ObjectPropertyBrowserAlias("BPL." + nameof(StrID))]
		[ObjectPropertyBrowserShow]
		public string StrID => ReferenceBulletPallete?.StrID;

		private string setStrID;
		[ObjectPropertyBrowserShow]
		[ObjectPropertyBrowserTipText("ObjectPalleteStrId")]
		[ObjectPropertyBrowserAlias("SetStrID")]
		public string SetStrID
		{
			get => setStrID;
			set
			{
				Set(ref setStrID, value);
				setStrID = default;
			}
		}

		[ObjectPropertyBrowserAlias("BPL." + nameof(PlaceOffset))]
		[ObjectPropertyBrowserShow]
		public int PlaceOffset => ReferenceBulletPallete?.PlaceOffset ?? default;

		[ObjectPropertyBrowserAlias("BPL." + nameof(TypeValue))]
		[ObjectPropertyBrowserShow]
		public BulletType TypeValue => ReferenceBulletPallete?.TypeValue ?? default;

		[ObjectPropertyBrowserAlias("BPL." + nameof(TargetValue))]
		[ObjectPropertyBrowserShow]
		public Target TargetValue => ReferenceBulletPallete?.TargetValue ?? default;

		[ObjectPropertyBrowserAlias("BPL." + nameof(ShooterValue))]
		[ObjectPropertyBrowserShow]
		public Shooter ShooterValue => ReferenceBulletPallete?.ShooterValue ?? default;

		[ObjectPropertyBrowserAlias("BPL." + nameof(SizeValue))]
		[ObjectPropertyBrowserShow]
		public BulletSize SizeValue => ReferenceBulletPallete?.SizeValue ?? default;

		public override IEnumerable<IDisplayableObject> GetDisplayableObjects()
		{
			yield return this;
		}

		public override void Copy(OngekiObjectBase fromObj)
		{
			base.Copy(fromObj);

			if (fromObj is not Bell from)
				return;

			ReferenceBulletPallete = from.ReferenceBulletPallete;
		}
	}
}
