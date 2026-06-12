using OngekiFumenEditor.Base.EditorObjects;
using OngekiFumenEditor.Base.OngekiObjects.Lane;
using OngekiFumenEditor.Base.OngekiObjects.Projectiles.Enums;
using System;
using System.Linq;
using OngekiFumenEditor.Base.ValueTypes;

namespace OngekiFumenEditor.Base.OngekiObjects
{
    public class BulletPallete : OngekiObjectBase
    {
        public static int RandomSeed { get; set; } = DateTime.Now.ToString().GetHashCode();

        public string StrID
        {
            get;
            set
            {
                field = value;
                NotifyOfPropertyChange(() => StrID);
            }
        }

        public string EditorName
        {
            get;
            set
            {
                field = value;
                NotifyOfPropertyChange(() => EditorName);
            }
        } = string.Empty;

        public Color EditorAxuiliaryLineColor
        {
            get;
            set
            {
                field = value;
                NotifyOfPropertyChange(() => EditorAxuiliaryLineColor);
            }
        } = Colors.DarkKhaki;

        public Shooter ShooterValue
        {
            get;
            set
            {
                field = value;
                NotifyOfPropertyChange(() => ShooterValue);
            }
        } = Shooter.Center;

        public int PlaceOffset
        {
            get;
            set
            {
                field = value;
                NotifyOfPropertyChange(() => PlaceOffset);
            }
        } = default;

        public int RandomOffsetRange
        {
            get;
            set
            {
                field = value;
                NotifyOfPropertyChange(() => RandomOffsetRange);
            }
        } = default;

        public Target TargetValue
        {
            get;
            set
            {
                field = value;
                NotifyOfPropertyChange(() => TargetValue);
            }
        } = Target.FixField;

        public BulletSize SizeValue
        {
            get;
            set => Set(ref field, value);
        } = BulletSize.Normal;

        public BulletType TypeValue
        {
            get;
            set => Set(ref field, value);
        } = BulletType.Circle;

        public float Speed
        {
            get;
            set
            {
                field = value;
                NotifyOfPropertyChange(() => Speed);
            }
        } = 1;

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
