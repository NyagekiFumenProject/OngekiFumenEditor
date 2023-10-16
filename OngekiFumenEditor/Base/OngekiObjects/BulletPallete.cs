using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Media;

namespace OngekiFumenEditor.Base.OngekiObjects
{
    public class BulletPallete : OngekiObjectBase
    {
        public enum Shooter
        {
            /// <summary>
            /// 从玩家头顶位置
            /// </summary>
            TargetHead = 0,
            /// <summary>
            /// 从敌人位置
            /// </summary>
            Enemy = 1,
            /// <summary>
            /// 谱面中心(?)
            /// </summary>
            Center = 2,
        }

        public enum Target
        {
            /// <summary>
            /// 射向玩家位置
            /// </summary>
            Player = 0,
            /// <summary>
            /// 射向对应位置，具体看使用的BLT指令的xUnit值
            /// </summary>
            FixField = 1
        }

        public enum BulletSize
        {
            /// <summary>
            /// 普通大小
            /// </summary>
            Normal = 0,
            /// <summary>
            /// 加大版(是普通版的1.4x)
            /// </summary>
            Large = 1,
        }

        public enum BulletType
        {
            /// <summary>
            /// 圆形子弹
            /// </summary>
            Circle = 0,
            /// <summary>
            /// 针状子弹
            /// </summary>
            Needle = 1,
            /// <summary>
            /// 圆柱形(方状)子弹
            /// </summary>
            Square = 2,
        }

        public double CalculateToXGrid(double xGridTotalUnit, OngekiFumen fumen)
        {
            var xUnit = 0d;

            //暂时实现Target.FixField的
            if (TargetValue == Target.FixField)
            {
                xUnit = xGridTotalUnit;
            }
            else if (TargetValue == Target.Player)
            {
                //写死先
                xUnit = 0;
            }

            return xUnit;
        }

        public XGrid CalculateToXGrid(XGrid xGrid, OngekiFumen fumen)
        {
            xGrid = new XGrid((float)CalculateToXGrid(xGrid.TotalUnit,fumen));
            xGrid.NormalizeSelf();
            return xGrid;
        }

        public double CalculateFromXGrid(double xGridTotalUnit, OngekiFumen fumen)
        {
            var xUnit = 0d;

            //暂时实现Shooter.TargetHead && Target.FixField的
            if (ShooterValue == Shooter.TargetHead &
                TargetValue == Target.FixField)
            {
                xUnit = xGridTotalUnit;
            }

            xUnit += PlaceOffset;
            return xUnit;
        }

        public XGrid CalculateFromXGrid(XGrid xGrid, OngekiFumen fumen)
        {
            xGrid = new XGrid((float)CalculateFromXGrid(xGrid.TotalUnit, fumen));
            xGrid.NormalizeSelf();
            return xGrid;
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

        public override string ToString() => $"{base.ToString()} StrID[{StrID}] Speed[{Speed:F3}] ShooterValue[{ShooterValue}] TargetValue[{TargetValue}] SizeValue[{SizeValue}] TypeValue[{TypeValue}]";

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
