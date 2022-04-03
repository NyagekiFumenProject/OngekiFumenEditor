using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace OngekiFumenEditor.Base.OngekiObjects
{
    public class BulletPallete : OngekiObjectBase
    {
        public class Shooter : FadeStringEnum
        {
            /// <summary>
            /// 从玩家头顶位置
            /// </summary>
            public static Shooter TargetHead { get; } = new Shooter("UPS");
            /// <summary>
            /// 从敌人位置
            /// </summary>
            public static Shooter Enemy { get; } = new Shooter("ENE");
            /// <summary>
            /// 谱面中心(?)
            /// </summary>
            public static Shooter Center { get; } = new Shooter("CEN");

            public Shooter(string value) : base(value)
            {
            }
        }

        public class Target : FadeStringEnum
        {
            public Target(string value) : base(value)
            {
            }

            /// <summary>
            /// 射向玩家位置
            /// </summary>
            public static Target Player { get; } = new Target("PLR");
            /// <summary>
            /// 射向对应位置，具体看使用的BLT指令的xUnit值
            /// </summary>
            public static Target FixField { get; } = new Target("FIX");
        }

        public XGrid CalculateToXGrid(XGrid xGrid, OngekiFumen fumen)
        {
            var xUnit = 0f;

            //暂时实现Target.FixField的
            if (TargetValue == Target.FixField)
            {
                xUnit = xGrid.Unit;
            }
            else if (TargetValue == Target.Player)
            {
                //写死先
                xUnit = 0;
            }

            xGrid = new XGrid(xUnit);
            xGrid.NormalizeSelf();
            return xGrid;
        }

        public XGrid CalculateFromXGrid(XGrid xGrid, OngekiFumen fumen)
        {
            var xUnit = 0f;

            //暂时实现Shooter.TargetHead && Target.FixField的
            if (ShooterValue == Shooter.TargetHead &
                TargetValue == Target.FixField)
            {
                xUnit = xGrid.Unit;
            }

            xUnit += PlaceOffset;
            xGrid = new XGrid(xUnit);
            xGrid.NormalizeSelf();
            return xGrid;
        }

        public class BulletSize : FadeStringEnum
        {
            public BulletSize(string value) : base(value)
            {

            }

            /// <summary>
            /// 普通大小
            /// </summary>
            public static BulletSize Normal { get; } = new("N");
            /// <summary>
            /// 加大版(是普通版的1.4x)
            /// </summary>
            public static BulletSize Large { get; } = new("L");
        }

        public class BulletType : FadeStringEnum
        {
            public BulletType(string value) : base(value)
            {

            }

            /// <summary>
            /// 圆形子弹
            /// </summary>
            public static BulletType Circle { get; } = new("CIR");
            /// <summary>
            /// 针状子弹
            /// </summary>
            public static BulletType Needle { get; } = new("NDL");
            /// <summary>
            /// 圆柱形(方状)子弹
            /// </summary>
            public static BulletType Square { get; } = new("SQR");
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

        public static string CommandName => "BPL";
        public override string IDShortName => CommandName;

        public override void Copy(OngekiObjectBase fromObj, OngekiFumen fumen)
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
