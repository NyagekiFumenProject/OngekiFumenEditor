using OngekiFumenEditor.Base.EditorObjects;
using OngekiFumenEditor.Base.OngekiObjects.BulletPalleteEnums;
using OngekiFumenEditor.Base.OngekiObjects.Lane;
using System;
using System.Linq;
using System.Windows.Media;

namespace OngekiFumenEditor.Base.OngekiObjects
{
    public class BulletPallete : OngekiObjectBase
    {
        public static int RandomSeed { get; set; } = DateTime.Now.ToString().GetHashCode();

        public double CalculateToXGridTotalUnit(IBulletPalleteReferencable refObject, OngekiFumen fumen)
        {
            double xGridTotalUnit;
            switch (TargetValue)
            {
                case Target.Player:
                    var tGrid = refObject.TGrid;
                    var apfLane = fumen.Lanes.GetVisibleStartObjects(tGrid, tGrid).OfType<AutoplayFaderLaneStart>().LastOrDefault();
                    var xGrid = apfLane?.CalulateXGrid(tGrid);
                    xGridTotalUnit = xGrid?.TotalUnit ?? 0d;
                    xGridTotalUnit += refObject.XGrid.TotalUnit;
                    break;
                case Target.FixField:
                    xGridTotalUnit = refObject.XGrid.TotalUnit;
                    break;
                default:
                    xGridTotalUnit = 0d;
                    break;
            }

            var randomOffset = 0;
            if (RandomOffsetRange > 0)
            {
                var id = ((OngekiObjectBase)refObject).Id;
                //不想用Random类，直接异或计算吧
                var seed = Math.Abs((RandomSeed * id + 123) * id ^ id);
                var r = RandomOffsetRange;
                randomOffset = (-r) + (seed % (r - (-r) + 1));
            }

            return xGridTotalUnit + randomOffset;
        }

        public double CalculateFromXGridTotalUnit(IBulletPalleteReferencable refObject, OngekiFumen fumen)
        {
            double xGridTotalUnit;

            switch (ShooterValue)
            {
                case Shooter.TargetHead:
                    xGridTotalUnit = refObject.XGrid.TotalUnit;
                    break;
                case Shooter.Enemy:
                    var tGrid = refObject.TGrid;
                    var enemyLane = fumen.Lanes.GetVisibleStartObjects(tGrid, tGrid).OfType<EnemyLaneStart>().LastOrDefault();
                    var xGrid = enemyLane?.CalulateXGrid(tGrid);
                    xGridTotalUnit = xGrid?.TotalUnit ?? refObject.XGrid.TotalUnit;
                    break;
                case Shooter.Center:
                default:
                    xGridTotalUnit = 0d;
                    break;
            }

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

        private int randomOffsetRange = default;
        public int RandomOffsetRange
        {
            get { return randomOffsetRange; }
            set
            {
                randomOffsetRange = value;
                NotifyOfPropertyChange(() => RandomOffsetRange);
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

        public override string ToString() => $"{base.ToString()} StrID[{StrID}] Speed[{Speed:F3}] ShooterValue[{ShooterValue}] TargetValue[{TargetValue}] SizeValue[{SizeValue}] TypeValue[{TypeValue}] EditorName[{EditorName}] PlaceOffset[{PlaceOffset}] RandomOffsetRange[{RandomOffsetRange}]";

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
            RandomOffsetRange = fromBpl.RandomOffsetRange;
        }
    }
}
