using OngekiFumenEditor.Avalonia.Base.OngekiObjects.Projectiles.Enums;

namespace OngekiFumenEditor.Avalonia.Base.OngekiObjects
{
    public class BulletPallete : OngekiObjectBase
    {
        public static int RandomSeed { get; set; } = DateTime.Now.ToString().GetHashCode();

        public static BulletPallete DummyCustomPallete { get; } = new BulletPallete()
        {
            StrID = "----",
            EditorName = "自定义无模板",
        };

        static BulletPallete()
        {
            DummyCustomPallete.PropertyChanged += (s, e) => throw new InvalidOperationException("DummyCustomPallete can't be modify");
        }

        private string strID = string.Empty;
        public string StrID
        {
            get { return strID; }
            set
            {
                strID = value;
                OnPropertyChanged(() => StrID);
            }
        }

        private string editorName = string.Empty;
        public string EditorName
        {
            get { return editorName; }
            set
            {
                editorName = value;
                OnPropertyChanged(() => EditorName);
            }
        }

        private Color editorAxuiliaryLineColor = Colors.DarkKhaki;
        public Color EditorAxuiliaryLineColor
        {
            get { return editorAxuiliaryLineColor; }
            set
            {
                editorAxuiliaryLineColor = value;
                OnPropertyChanged(() => EditorAxuiliaryLineColor);
            }
        }

        private Shooter shooterValue = Shooter.Center;
        public Shooter ShooterValue
        {
            get { return shooterValue; }
            set
            {
                shooterValue = value;
                OnPropertyChanged(() => ShooterValue);
            }
        }

        private int placeOffset = default;
        public int PlaceOffset
        {
            get { return placeOffset; }
            set
            {
                placeOffset = value;
                OnPropertyChanged(() => PlaceOffset);
            }
        }

        private int randomOffsetRange = default;
        public int RandomOffsetRange
        {
            get { return randomOffsetRange; }
            set
            {
                randomOffsetRange = value;
                OnPropertyChanged(() => RandomOffsetRange);
            }
        }

        private Target targetValue = Target.FixField;
        public Target TargetValue
        {
            get { return targetValue; }
            set
            {
                targetValue = value;
                OnPropertyChanged(() => TargetValue);
            }
        }

        private BulletSize sizeValue = BulletSize.Normal;
        public BulletSize SizeValue
        {
            get => sizeValue;
            set => SetProperty(ref sizeValue, value);
        }

        private BulletType typeValue = BulletType.Circle;
        public BulletType TypeValue
        {
            get => typeValue;
            set => SetProperty(ref typeValue, value);
        }

        private float speed = 1;
        public float Speed
        {
            get { return speed; }
            set
            {
                speed = value;
                OnPropertyChanged(() => Speed);
            }
        }

        /// <summary>
        /// 是否受到变速影响
        /// </summary>
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
