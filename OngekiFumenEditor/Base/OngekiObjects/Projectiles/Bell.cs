using System;
using OngekiFumenEditor.Base.Attributes;
using OngekiFumenEditor.Base.OngekiObjects.Projectiles.Attributes;
using OngekiFumenEditor.Base.OngekiObjects.Projectiles.Enums;
using System.Collections.Generic;

namespace OngekiFumenEditor.Base.OngekiObjects.Projectiles
{
    public partial class Bell : OngekiMovableObjectBase, IBulletPalleteReferencable, IProjectile
    {
        public const string OngekiDefaultBellPaletteName = "--";

        public static string CommandName => "BEL";
        public static string CustomCommandName => "[CUSTOM_BEL]";

        public override string IDShortName => CommandName;

        [LocalizableObjectPropertyBrowserAlias("BulletPalleteDisplayName")]
        public BulletPallete? ReferenceBulletPallete
        {
            get;
            set
            {
                //Log.LogDebug($"bell(id:{Id})'s pallete has been changed from {referenceBulletPallete?.StrID} to {value?.StrID}");
                this.RegisterOrUnregisterPropertyChangeEvent(field, value, ReferenceBulletPallete_PropertyChanged);
                Set(ref field, value);

                NotifyOfPropertyChange(() => Speed);
                NotifyOfPropertyChange(() => PlaceOffset);
                NotifyOfPropertyChange(() => TargetValue);
                NotifyOfPropertyChange(() => ShooterValue);
                NotifyOfPropertyChange(() => RandomOffsetRange);
                NotifyOfPropertyChange(() => IsEnableSoflan);
            }
        }

        private void ReferenceBulletPallete_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(BulletPallete.StrID):
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
        public Shooter ShooterValue
        {
            get => ReferenceBulletPallete?.ShooterValue ?? field;
            set => Set(ref field, value);
        } = Shooter.TargetHead;

        [ObjectPropertyBrowserHide] // SizeValue has no effect on bells
        public BulletSize SizeValue
        {
            get => ReferenceBulletPallete?.SizeValue ?? field;
            set => Set(ref field, value);
        } = BulletSize.Normal;

        [ObjectPropertyBrowserHide] // TargetValue has no effect on Bells
        public Target TargetValue
        {
            get => ReferenceBulletPallete?.TargetValue ?? field;
            set => Set(ref field, value);
        } = Target.FixField;

        // Unlike bullets, bells cannot target the player so soflan always applies to them
        public bool IsEnableSoflan => true;

        public BulletType TypeValue => BulletType.Circle;

        public override IEnumerable<IDisplayableObject> GetDisplayableObjects()
        {
            yield return this;
        }

        public override void Copy(OngekiObjectBase fromObj)
        {
            base.Copy(fromObj);

            if (fromObj is not Bell from)
                return;

            if (from.ReferenceBulletPallete is null) {
                PlaceOffset = from.PlaceOffset;
                RandomOffsetRange = from.RandomOffsetRange;
                ShooterValue = from.ShooterValue;
                Speed = from.Speed;
                TargetValue = from.TargetValue;
                SizeValue = from.SizeValue;
            }
            else {
                ReferenceBulletPallete = from.ReferenceBulletPallete;
            }
        }

        public bool IsOngekiDefaultBell()
        {
            return this is
            {
                ReferenceBulletPallete: null,
                PlaceOffset: 0,
                RandomOffsetRange: 0,
                ShooterValue: Shooter.TargetHead,
                Speed: 1,
                SizeValue: BulletSize.Normal,
                TargetValue: Target.FixField
            };
        }
    }
}
