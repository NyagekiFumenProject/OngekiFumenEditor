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
        [LocalizableObjectPropertyBrowserAlias("BulletPalleteDisplayName")]
        public BulletPallete? ReferenceBulletPallete
        {
            get;
            set
            {
                //Log.LogDebug($"bullet(id:{Id})'s pallete has been changed from {referenceBulletPallete?.StrID} to {value?.StrID}");
                this.RegisterOrUnregisterPropertyChangeEvent(field, value, ReferenceBulletPallete_PropertyChanged);
                Set(ref field, value);

                NotifyOfPropertyChange(() => Speed);
                NotifyOfPropertyChange(() => PlaceOffset);
                NotifyOfPropertyChange(() => TypeValue);
                NotifyOfPropertyChange(() => TargetValue);
                NotifyOfPropertyChange(() => ShooterValue);
                NotifyOfPropertyChange(() => SizeValue);
                NotifyOfPropertyChange(() => RandomOffsetRange);
                NotifyOfPropertyChange(() => IsEnableSoflan);
            }
        } = null;

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

        public BulletDamageType BulletDamageTypeValue
        {
            get;
            set
            {
                field = value;
                NotifyOfPropertyChange(() => BulletDamageTypeValue);
            }
        } = BulletDamageType.Normal;

        [ObjectPropertyBrowserShow]
        [ProjectilePropertyBrowserReadOnlyForPalleteIsSet]
        public float Speed
        {
            get => ReferenceBulletPallete?.Speed ?? field;
            set => Set(ref field, value);
        } = 1;

        [ObjectPropertyBrowserShow]
        [ProjectilePropertyBrowserReadOnlyForPalleteIsSet]
        public int RandomOffsetRange
        {
            get => ReferenceBulletPallete?.RandomOffsetRange ?? field;
            set => Set(ref field, value);
        } = 0;

        [ObjectPropertyBrowserShow]
        [ProjectilePropertyBrowserReadOnlyForPalleteIsSet]
        public int PlaceOffset
        {
            get => ReferenceBulletPallete?.PlaceOffset ?? field;
            set => Set(ref field, value);
        } = 0;

        [ObjectPropertyBrowserShow]
        [ProjectilePropertyBrowserReadOnlyForPalleteIsSet]
        public BulletType TypeValue
        {
            get => ReferenceBulletPallete?.TypeValue ?? field;
            set => Set(ref field, value);
        } = BulletType.Circle;

        [ObjectPropertyBrowserShow]
        [ProjectilePropertyBrowserReadOnlyForPalleteIsSet]
        public Target TargetValue
        {
            get => ReferenceBulletPallete?.TargetValue ?? field;
            set
            {
                Set(ref field, value);
                NotifyOfPropertyChange(() => IsEnableSoflan);
            }
        } = Target.Player;

        [ObjectPropertyBrowserShow]
        [ProjectilePropertyBrowserReadOnlyForPalleteIsSet]
        public Shooter ShooterValue
        {
            get => ReferenceBulletPallete?.ShooterValue ?? field;
            set => Set(ref field, value);
        } = Shooter.Center;

        [ObjectPropertyBrowserShow]
        [ProjectilePropertyBrowserReadOnlyForPalleteIsSet]
        public BulletSize SizeValue
        {
            get => ReferenceBulletPallete?.SizeValue ?? field;
            set => Set(ref field, value);
        } = BulletSize.Normal;

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

            if (from.ReferenceBulletPallete is null) {
                PlaceOffset = from.PlaceOffset;
                RandomOffsetRange = from.RandomOffsetRange;
                ShooterValue = from.ShooterValue;
                SizeValue = from.SizeValue;
                Speed = from.Speed;
                TargetValue = from.TargetValue;
                TypeValue = from.TypeValue;
            }
            else {
                ReferenceBulletPallete = from.ReferenceBulletPallete;
            }

            BulletDamageTypeValue = from.BulletDamageTypeValue;
        }
    }
}

