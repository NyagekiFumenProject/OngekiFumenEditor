using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Base.OngekiObjects
{
    public class BulletPalleteList : OngekiObjectBase
    {
        public class Shooter : FadeStringEnum
        {
            public static implicit operator string(Shooter s)
            {
                return s.Value;
            }

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
            public static implicit operator string(Target s)
            {
                return s.Value;
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

        public class BulletType : FadeStringEnum
        {
            public BulletType(string value) : base(value)
            {
            }

            public static implicit operator string(BulletType s)
            {
                return s.Value;
            }

            /// <summary>
            /// 将使用BULLET_DAMAGE伤害
            /// </summary>
            public static BulletType Normal { get; } = new BulletType("NML");
            /// <summary>
            /// 将使用HARDBULLET_DAMAGE伤害
            /// </summary>
            public static BulletType Hard { get; } = new BulletType("STR");
            /// <summary>
            /// 将使用DANGERBULLET_DAMAGE伤害
            /// </summary>
            public static BulletType Danger { get; } = new BulletType("DNG");
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

        private float speed = default;
        public float Speed
        {
            get { return speed; }
            set
            {
                speed = value;
                NotifyOfPropertyChange(() => Speed);
            }
        }

        private BulletType bulletTypeValue = BulletType.Normal;
        public BulletType BulletTypeValue
        {
            get { return bulletTypeValue; }
            set
            {
                bulletTypeValue = value;
                NotifyOfPropertyChange(() => BulletTypeValue);
            }
        }

        public static string CommandName => "BPL";
        public override string IDShortName => CommandName;

        public override string Serialize(OngekiFumen fumenData)
        {
            return $"{IDShortName} {StrID} {ShooterValue} {PlaceOffset} {TargetValue} {Speed} {BulletTypeValue}";
        }
    }
}
