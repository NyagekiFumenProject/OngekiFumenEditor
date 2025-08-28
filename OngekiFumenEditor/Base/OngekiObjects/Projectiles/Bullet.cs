using OngekiFumenEditor.Base.Attributes;
using OngekiFumenEditor.Base.OngekiObjects.Projectiles;
using OngekiFumenEditor.Base.OngekiObjects.Projectiles.Attributes;
using OngekiFumenEditor.Base.OngekiObjects.Projectiles.Enums;
using OngekiFumenEditor.Utils;
using System.Collections.Generic;
using static OngekiFumenEditor.Base.OngekiObjects.BulletPallete;

namespace OngekiFumenEditor.Base.OngekiObjects
{
    //[DontShowPropertyInfoAttrbute]
    public partial class Bullet : OngekiMovableObjectBase, IBulletPalleteReferencable, IProjectile
    {
        private BulletPallete referenceBulletPallete;
        public BulletPallete ReferenceBulletPallete
        {
            get { return referenceBulletPallete; }
            set
            {
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
        [PropertyBrowserReadOnlyForPalleteNotNull]
        public float Speed
        {
            get => ReferenceBulletPallete?.Speed ?? localSpeed;
            set => Set(ref localSpeed, value);
        }

        private float localRandomOffsetRange = 0f;
        [ObjectPropertyBrowserShow]
        [PropertyBrowserReadOnlyForPalleteNotNull]
        public float RandomOffsetRange
        {
            get => ReferenceBulletPallete?.RandomOffsetRange ?? localRandomOffsetRange;
            set => Set(ref localRandomOffsetRange, value);
        }

        private int localPlaceOffset = 0;
        [ObjectPropertyBrowserShow]
        [PropertyBrowserReadOnlyForPalleteNotNull]
        public int PlaceOffset
        {
            get => ReferenceBulletPallete?.PlaceOffset ?? localPlaceOffset;
            set => Set(ref localPlaceOffset, value);
        }

        private BulletType localTypeValue = BulletType.Circle;
        [ObjectPropertyBrowserShow]
        [PropertyBrowserReadOnlyForPalleteNotNull]
        public BulletType TypeValue
        {
            get => ReferenceBulletPallete?.TypeValue ?? localTypeValue;
            set => Set(ref localTypeValue, value);
        }

        private Target localTargetValue = Target.FixField;
        [ObjectPropertyBrowserShow]
        [PropertyBrowserReadOnlyForPalleteNotNull]
        public Target TargetValue
        {
            get => ReferenceBulletPallete?.TargetValue ?? localTargetValue;
            set
            {
                Set(ref localTargetValue, value);
                NotifyOfPropertyChange(() => IsEnableSoflan);
            }
        }

        private Shooter localShooterValue = Shooter.TargetHead;
        [ObjectPropertyBrowserShow]
        [PropertyBrowserReadOnlyForPalleteNotNull]
        public Shooter ShooterValue
        {
            get => ReferenceBulletPallete?.ShooterValue ?? localShooterValue;
            set => Set(ref localShooterValue, value);
        }

        private BulletSize localSizeValue = BulletSize.Normal;
        [ObjectPropertyBrowserShow]
        [PropertyBrowserReadOnlyForPalleteNotNull]
        public BulletSize SizeValue
        {
            get => ReferenceBulletPallete?.SizeValue ?? localSizeValue;
            set => Set(ref localSizeValue, value);
        }

        public bool IsEnableSoflan => ReferenceBulletPallete?.IsEnableSoflan ?? (TargetValue != Target.Player);

        public override string IDShortName => CommandName;

        public const string CommandName = "BLT";

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
