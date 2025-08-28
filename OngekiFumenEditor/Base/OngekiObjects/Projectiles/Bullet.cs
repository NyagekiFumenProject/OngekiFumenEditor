using OngekiFumenEditor.Base.Attributes;
using OngekiFumenEditor.Base.OngekiObjects.Projectiles;
using OngekiFumenEditor.Base.OngekiObjects.Projectiles.Attributes;
using OngekiFumenEditor.Base.OngekiObjects.Projectiles.Enums;
using OngekiFumenEditor.Utils;
using System.Collections.Generic;

namespace OngekiFumenEditor.Base.OngekiObjects
{
    //[DontShowPropertyInfoAttrbute]
    public partial class Bullet : OngekiMovableObjectBase, IBulletPalleteReferencable, IProjectile
    {
        bool IsReferenceValidPallete => ReferenceBulletPallete != null && ReferenceBulletPallete != BulletPallete.DummyCustomPallete;

        private BulletPallete referenceBulletPallete;
        [ObjectPropertyBrowserAlias("子弹模板")]
        public BulletPallete ReferenceBulletPallete
        {
            get { return referenceBulletPallete; }
            set
            {
                Log.LogDebug($"bullet(id:{Id})'s pallete has been changed from {referenceBulletPallete?.StrID} to {value?.StrID}");
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
        [PropertyBrowserReadOnlyForPalleteIsValid]
        public float Speed
        {
            get => IsReferenceValidPallete ? ReferenceBulletPallete.Speed : localSpeed;
            set => Set(ref localSpeed, value);
        }

        private float localRandomOffsetRange = 0f;
        [ObjectPropertyBrowserShow]
        [PropertyBrowserReadOnlyForPalleteIsValid]
        public float RandomOffsetRange
        {
            get => IsReferenceValidPallete ? ReferenceBulletPallete.RandomOffsetRange : localRandomOffsetRange;
            set => Set(ref localRandomOffsetRange, value);
        }

        private int localPlaceOffset = 0;
        [ObjectPropertyBrowserShow]
        [PropertyBrowserReadOnlyForPalleteIsValid]
        public int PlaceOffset
        {
            get => IsReferenceValidPallete ? ReferenceBulletPallete.PlaceOffset : localPlaceOffset;
            set => Set(ref localPlaceOffset, value);
        }

        private BulletType localTypeValue = BulletType.Circle;
        [ObjectPropertyBrowserShow]
        [PropertyBrowserReadOnlyForPalleteIsValid]
        public BulletType TypeValue
        {
            get => IsReferenceValidPallete ? ReferenceBulletPallete.TypeValue : localTypeValue;
            set => Set(ref localTypeValue, value);
        }

        private Target localTargetValue = Target.FixField;
        [ObjectPropertyBrowserShow]
        [PropertyBrowserReadOnlyForPalleteIsValid]
        public Target TargetValue
        {
            get => IsReferenceValidPallete ? ReferenceBulletPallete.TargetValue : localTargetValue;
            set
            {
                Set(ref localTargetValue, value);
                NotifyOfPropertyChange(() => IsEnableSoflan);
            }
        }

        private Shooter localShooterValue = Shooter.TargetHead;
        [ObjectPropertyBrowserShow]
        [PropertyBrowserReadOnlyForPalleteIsValid]
        public Shooter ShooterValue
        {
            get => IsReferenceValidPallete ? ReferenceBulletPallete.ShooterValue : localShooterValue;
            set => Set(ref localShooterValue, value);
        }

        private BulletSize localSizeValue = BulletSize.Normal;
        [ObjectPropertyBrowserShow]
        [PropertyBrowserReadOnlyForPalleteIsValid]
        public BulletSize SizeValue
        {
            get => IsReferenceValidPallete ? ReferenceBulletPallete.SizeValue : localSizeValue;
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
