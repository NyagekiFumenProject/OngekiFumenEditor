using OngekiFumenEditor.Base.OngekiObjects.BulletPalleteEnums;
using System.Windows.Media;

namespace OngekiFumenEditor.Base.OngekiObjects
{
	public class BulletPallete : OngekiObjectBase
	{
		public double CalculateToXGridTotalUnit(IBulletPalleteReferencable refObject, OngekiFumen fumen)
		{
			var xGridTotalUnit = 0d;

			switch (TargetValue)
			{
				case Target.Player:
					xGridTotalUnit = refObject.XGrid.TotalUnit;
					break;
				case Target.FixField:
					xGridTotalUnit = refObject.XGrid.TotalUnit;
					break;
				default:
					break;
			}

			return xGridTotalUnit;
		}

		public double CalculateFromXGridTotalUnit(IBulletPalleteReferencable refObject, OngekiFumen fumen)
		{
			var xGridTotalUnit = 0d;

			//暂时实现Shooter.TargetHead && Target.FixField的
			if (ShooterValue == Shooter.TargetHead)
				xGridTotalUnit = refObject.XGrid.TotalUnit;

			xGridTotalUnit += PlaceOffset;
			return xGridTotalUnit;
		}

		private string strID = string.Empty;
		public string StrID
		{
			get { return strID; }
			set
			{
				strID = value;
				NotifyOfPropertyChange(() => StrID);
			}
		}

		private string editorName = string.Empty;
		public string EditorName
		{
			get { return editorName; }
			set
			{
				editorName = value;
				NotifyOfPropertyChange(() => EditorName);
			}
		}

		private Color editorAxuiliaryLineColor = Colors.DarkKhaki;
		public Color EditorAxuiliaryLineColor
		{
			get { return editorAxuiliaryLineColor; }
			set
			{
				editorAxuiliaryLineColor = value;
				NotifyOfPropertyChange(() => EditorAxuiliaryLineColor);
			}
		}

		private Shooter shooterValue = Shooter.Center;
		public Shooter ShooterValue
		{
			get { return shooterValue; }
			set
			{
				shooterValue = value;
				NotifyOfPropertyChange(() => ShooterValue);
			}
		}

		private int placeOffset = default;
		public int PlaceOffset
		{
			get { return placeOffset; }
			set
			{
				placeOffset = value;
				NotifyOfPropertyChange(() => PlaceOffset);
			}
		}

		private Target targetValue = Target.FixField;
		public Target TargetValue
		{
			get { return targetValue; }
			set
			{
				targetValue = value;
				NotifyOfPropertyChange(() => TargetValue);
			}
		}

		private BulletSize sizeValue = BulletSize.Normal;
		public BulletSize SizeValue
		{
			get => sizeValue;
			set => Set(ref sizeValue, value);
		}

		private BulletType typeValue = BulletType.Circle;
		public BulletType TypeValue
		{
			get => typeValue;
			set => Set(ref typeValue, value);
		}

		private float speed = 1;
		public float Speed
		{
			get { return speed; }
			set
			{
				speed = value;
				NotifyOfPropertyChange(() => Speed);
			}
		}

		public bool IsEnableSoflan => TargetValue != Target.Player;

		public override string ToString() => $"{base.ToString()} StrID[{StrID}] Speed[{Speed:F3}] ShooterValue[{ShooterValue}] TargetValue[{TargetValue}] SizeValue[{SizeValue}] TypeValue[{TypeValue}] EditorName[{EditorName}] PlaceOffset[{PlaceOffset}]";

		public static string CommandName => "BPL";
		public override string IDShortName => CommandName;

		public override void Copy(OngekiObjectBase fromObj)
		{
			if (fromObj is not BulletPallete fromBpl)
				return;

			PlaceOffset = fromBpl.PlaceOffset;
			StrID = fromBpl.StrID;
			SizeValue = fromBpl.SizeValue;
			TypeValue = fromBpl.TypeValue;
			EditorName = fromBpl.EditorName;
			EditorAxuiliaryLineColor = fromBpl.EditorAxuiliaryLineColor;
			ShooterValue = fromBpl.ShooterValue;
			Speed = fromBpl.Speed;
			TargetValue = fromBpl.TargetValue;
		}
	}
}
