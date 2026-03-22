using OngekiFumenEditor.Base.Attributes;
using OngekiFumenEditor.Base.OngekiObjects.Projectiles;
using OngekiFumenEditor.Base.OngekiObjects.Projectiles.Attributes;
using OngekiFumenEditor.Base.OngekiObjects.Projectiles.Enums;
using OngekiFumenEditor.Utils;
using System.Collections.Generic;

namespace OngekiFumenEditor.Base.OngekiObjects.Projectiles
{
    //[DontShowPropertyInfoAttrbute]
    public partial class Bullet : OngekiMovableObjectBase, IBulletPalleteReferencable, IProjectile
    {
        bool IsUsePalleteValue => ReferenceBulletPallete != null && ReferenceBulletPallete != BulletPallete.DummyCustomPallete;

        private BulletPallete referenceBulletPallete;
        [LocalizableObjectPropertyBrowserAlias("BulletPalleteDisplayName")]
        public BulletPallete ReferenceBulletPallete
        {
            get { return referenceBulletPallete; }
            set
            {
                //Log.LogDebug($"bullet(id:{Id})'s pallete has been changed from {referenceBulletPallete?.StrID} to {value?.StrID}");
                this.RegisterOrUnregisterPropertyChangeEvent(referenceBulletPallete, value, ReferenceBulletPallete_PropertyChanged);
                Set(ref referenceBulletPallete, value);

                NotifyOfPropertyChange(() => Speed);
                NotifyOfPropertyChange(() => PlaceOffset);
                NotifyOfPropertyChange(() => TypeValue);
                NotifyOfPropertyChange(() => TargetValue);
                NotifyOfPropertyChange(() => ShooterValue);
                NotifyOfPropertyChange(() => SizeValue);
                NotifyOfPropertyChange(() => RandomOffsetRange);
                NotifyOfPropertyChange(() => IsEnableSoflan);
            }
        }

        private void ReferenceBulletPallete_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(BulletPallete.PlaceOffset):
                case nameof(BulletPallete.TypeValue):
                case nameof(BulletPallete.TargetValue):
                case nameof(BulletPallete.ShooterValue):
                case nameof(BulletPallete.SizeValue):
                case nameof(BulletPallete.Speed):
                case nameof(BulletPallete.IsEnableSoflan):
                case nameof(BulletPallete.RandomOffsetRange):
                    NotifyOfPropertyChange(e.PropertyName);
                    break;
            }
        }

        public override IEnumerable<IDisplayableObject> GetDisplayableObjects()
        {
            yield return this;
        }

        private BulletDamageType bulletDamageTypeValue = BulletDamageType.Normal;
        public BulletDamageType BulletDamageTypeValue
        {
            get { return bulletDamageTypeValue; }
            set
            {
                bulletDamageTypeValue = value;
                NotifyOfPropertyChange(() => BulletDamageTypeValue);
            }
        }

        private float localSpeed = 1f;
        [ObjectPropertyBrowserShow]
        [BulletPropertyBrowserReadOnlyForPalleteIsValid]
        public float Speed
        {
            get => IsUsePalleteValue ? ReferenceBulletPallete.Speed : localSpeed;
            set => Set(ref localSpeed, value);
        }

        private int localRandomOffsetRange = 0;
        [ObjectPropertyBrowserShow]
        [BulletPropertyBrowserReadOnlyForPalleteIsValid]
        public int RandomOffsetRange
        {
            get => IsUsePalleteValue ? ReferenceBulletPallete.RandomOffsetRange : localRandomOffsetRange;
            set => Set(ref localRandomOffsetRange, value);
        }

        private int localPlaceOffset = 0;
        [ObjectPropertyBrowserShow]
        [BulletPropertyBrowserReadOnlyForPalleteIsValid]
        public int PlaceOffset
        {
            get => IsUsePalleteValue ? ReferenceBulletPallete.PlaceOffset : localPlaceOffset;
            set => Set(ref localPlaceOffset, value);
        }

        private BulletType localTypeValue = BulletType.Circle;
        [ObjectPropertyBrowserShow]
        [BulletPropertyBrowserReadOnlyForPalleteIsValid]
        public BulletType TypeValue
        {
            get => IsUsePalleteValue ? ReferenceBulletPallete.TypeValue : localTypeValue;
            set => Set(ref localTypeValue, value);
        }

        private Target localTargetValue = Target.FixField;
        [ObjectPropertyBrowserShow]
        [BulletPropertyBrowserReadOnlyForPalleteIsValid]
        public Target TargetValue
        {
            get => IsUsePalleteValue ? ReferenceBulletPallete.TargetValue : localTargetValue;
            set
            {
                Set(ref localTargetValue, value);
                NotifyOfPropertyChange(() => IsEnableSoflan);
            }
        }

        private Shooter localShooterValue = Shooter.TargetHead;
        [ObjectPropertyBrowserShow]
        [BulletPropertyBrowserReadOnlyForPalleteIsValid]
        public Shooter ShooterValue
        {
            get => IsUsePalleteValue ? ReferenceBulletPallete.ShooterValue : localShooterValue;
            set => Set(ref localShooterValue, value);
        }

        private BulletSize localSizeValue = BulletSize.Normal;
        [ObjectPropertyBrowserShow]
        [BulletPropertyBrowserReadOnlyForPalleteIsValid]
        public BulletSize SizeValue
        {
            get => IsUsePalleteValue ? ReferenceBulletPallete.SizeValue : localSizeValue;
            set => Set(ref localSizeValue, value);
        }

        public bool IsEnableSoflan => ReferenceBulletPallete?.IsEnableSoflan ?? (TargetValue != Target.Player);

        public override string IDShortName => CommandName;

        public const string CommandName = "BLT";
        public const string CustomCommandName = "[CUSTOM_BLT]";

        public override string ToString() => $"{base.ToString()} Pallete[{ReferenceBulletPallete}] DamageType[{BulletDamageTypeValue}]";

        public override void Copy(OngekiObjectBase fromObj)
        {
            base.Copy(fromObj);

            if (fromObj is not Bullet from)
                return;

            ReferenceBulletPallete = from.ReferenceBulletPallete;
            BulletDamageTypeValue = from.BulletDamageTypeValue;

            localPlaceOffset = from.localPlaceOffset;
            localRandomOffsetRange = from.localRandomOffsetRange;
            localShooterValue = from.localShooterValue;
            localSizeValue = from.localSizeValue;
            localSpeed = from.localSpeed;
            localTargetValue = from.localTargetValue;
            localTypeValue = from.localTypeValue;
        }
    }
}
