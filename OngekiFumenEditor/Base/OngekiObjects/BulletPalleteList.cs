using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Base.OngekiObjects
{
    public class BulletPalleteList : IOngekiObject
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
            public readonly static Shooter TargetHead = new Shooter("UPS");
            /// <summary>
            /// 从敌人位置
            /// </summary>
            public readonly static Shooter Enemy = new Shooter("ENE");
            /// <summary>
            /// 谱面中心(?)
            /// </summary>
            public readonly static Shooter Center = new Shooter("CEN");

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
            public readonly static Target Player = new Target("PLR");
            /// <summary>
            /// 射向对应位置，具体看使用的BLT指令的xUnit值
            /// </summary>
            public readonly static Target FixField = new Target("FIX");
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
            public readonly static BulletType Normal = new BulletType("NML");
            /// <summary>
            /// 将使用HARDBULLET_DAMAGE伤害
            /// </summary>
            public readonly static BulletType Hard = new BulletType("STR");
            /// <summary>
            /// 将使用DANGERBULLET_DAMAGE伤害
            /// </summary>
            public readonly static BulletType Danger = new BulletType("DNG");
        }

        public string StrID { get; set; } = "";
        public Shooter ShooterValue { get; set; } = Shooter.Center;
        public int placeOffset { get; set; }
        public Target TargetValue { get; set; } = Target.FixField;
        public float Speed { get; set; }
        public BulletType BulletTypeValue { get; set; } = BulletType.Danger;

        public string Group => "B_PALETTE";
        public string IDShortName => "BPL";
        public string Name => "BulletPalleteList";

        public string Serialize(OngekiFumen fumenData)
        {
            return $"{IDShortName} {StrID} {ShooterValue} {placeOffset} {TargetValue} {Speed} {BulletTypeValue}";
        }
    }
}
