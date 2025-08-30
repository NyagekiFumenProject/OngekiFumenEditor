using OngekiFumenEditor.Base.Attributes;
using OngekiFumenEditor.Base.OngekiObjects.Projectiles;
using OngekiFumenEditor.Base.OngekiObjects.Projectiles.Attributes;
using OngekiFumenEditor.Base.OngekiObjects.Projectiles.Enums;
using OngekiFumenEditor.Utils;
using System.Collections.Generic;

namespace OngekiFumenEditor.Base.OngekiObjects
{
    public partial class Bell : OngekiMovableObjectBase, IBulletPalleteReferencable, IProjectile
    {

        public static string CommandName => "BEL";
        public static string CustomCommandName => "[CUSTOM_BEL]";

        public override string IDShortName => CommandName;

        public Bell()
        {
            ReferenceBulletPallete = null;
        }

        bool IsUsePalleteValue => ReferenceBulletPallete != null && ReferenceBulletPallete != BulletPallete.DummyCustomPallete;
        bool IsUseLocalCustomValue => ReferenceBulletPallete == BulletPallete.DummyCustomPallete;

        private BulletPallete referenceBulletPallete;
        [ObjectPropertyBrowserAlias("子弹模板")]
        public BulletPallete ReferenceBulletPallete
        {
            get { return referenceBulletPallete; }
            set
            {
                Log.LogDebug($"bell(id:{Id})'s pallete has been changed from {referenceBulletPallete?.StrID} to {value?.StrID}");
                this.RegisterOrUnregisterPropertyChangeEvent(referenceBulletPallete, value, ReferenceBulletPallete_PropertyChanged);
                Set(ref referenceBulletPallete, value);

                NotifyOfPropertyChange(() => Speed);
                NotifyOfPropertyChange(() => PlaceOffset);
                NotifyOfPropertyChange(() => TargetValue);
                NotifyOfPropertyChange(() => ShooterValue);
                NotifyOfPropertyChange(() => RandomOffsetRange);
                NotifyOfPropertyChange(() => SizeValue);
                NotifyOfPropertyChange(() => IsEnableSoflan);
            }
        }

        private void ReferenceBulletPallete_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
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

        private float localSpeed = 1f;
        [ObjectPropertyBrowserShow]
        [BellPropertyBrowserReadOnlyForPalleteIsNotDummyCustom]
        public float Speed
        {
            get => IsUsePalleteValue ? ReferenceBulletPallete.Speed : (IsUseLocalCustomValue ? localSpeed : 1f);
            set
            {
                Set(ref localSpeed, value);
                if (ReferenceBulletPallete == null)
                {
                    //auto set pallete to dummy if value is set and current pallete is null
                    ReferenceBulletPallete = BulletPallete.DummyCustomPallete;
                }
            }
        }

        private int localRandomOffsetRange = 0;
        [ObjectPropertyBrowserShow]
        [BellPropertyBrowserReadOnlyForPalleteIsNotDummyCustom]
        public int RandomOffsetRange
        {
            get => IsUsePalleteValue ? ReferenceBulletPallete.RandomOffsetRange : (IsUseLocalCustomValue ? localRandomOffsetRange : 0);
            set
            {
                Set(ref localRandomOffsetRange, value);
                if (ReferenceBulletPallete == null)
                {
                    //auto set pallete to dummy if value is set and current pallete is null
                    ReferenceBulletPallete = BulletPallete.DummyCustomPallete;
                }
            }
        }

        private int localPlaceOffset = 0;
        [ObjectPropertyBrowserShow]
        [BellPropertyBrowserReadOnlyForPalleteIsNotDummyCustom]
        public int PlaceOffset
        {
            get => IsUsePalleteValue ? ReferenceBulletPallete.PlaceOffset : (IsUseLocalCustomValue ? localPlaceOffset : 0);
            set
            {
                Set(ref localPlaceOffset, value);
                if (ReferenceBulletPallete == null)
                {
                    //auto set pallete to dummy if value is set and current pallete is null
                    ReferenceBulletPallete = BulletPallete.DummyCustomPallete;
                }
            }
        }

        private Target localTargetValue = Target.FixField;
        [ObjectPropertyBrowserShow]
        [BellPropertyBrowserReadOnlyForPalleteIsNotDummyCustom]
        public Target TargetValue
        {
            get => IsUsePalleteValue ? ReferenceBulletPallete.TargetValue : (IsUseLocalCustomValue ? localTargetValue : Target.FixField);
            set
            {
                Set(ref localTargetValue, value);
                NotifyOfPropertyChange(() => IsEnableSoflan);
                if (ReferenceBulletPallete == null)
                {
                    //auto set pallete to dummy if value is set and current pallete is null
                    ReferenceBulletPallete = BulletPallete.DummyCustomPallete;
                }
            }
        }

        private Shooter localShooterValue = Shooter.TargetHead;
        [ObjectPropertyBrowserShow]
        [BellPropertyBrowserReadOnlyForPalleteIsNotDummyCustom]
        public Shooter ShooterValue
        {
            get => IsUsePalleteValue ? ReferenceBulletPallete.ShooterValue : (IsUseLocalCustomValue ? localShooterValue : Shooter.TargetHead);
            set
            {
                Set(ref localShooterValue, value);
                if (ReferenceBulletPallete == null)
                {
                    //auto set pallete to dummy if value is set and current pallete is null
                    ReferenceBulletPallete = BulletPallete.DummyCustomPallete;
                }
            }
        }

        private BulletSize localSizeValue = BulletSize.Normal;
        [ObjectPropertyBrowserShow]
        [BellPropertyBrowserReadOnlyForPalleteIsNotDummyCustom]
        public BulletSize SizeValue
        {
            get => IsUsePalleteValue ? ReferenceBulletPallete.SizeValue : (IsUseLocalCustomValue ? localSizeValue : BulletSize.Normal);
            set
            {
                Set(ref localSizeValue, value);
                if (ReferenceBulletPallete == null)
                {
                    //auto set pallete to dummy if value is set and current pallete is null
                    ReferenceBulletPallete = BulletPallete.DummyCustomPallete;
                }
            }
        }

        /// <summary>
        /// 是否受到变速影响
        /// </summary>
        public bool IsEnableSoflan => ReferenceBulletPallete?.IsEnableSoflan ?? (TargetValue != Target.Player);

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

            ReferenceBulletPallete = from.ReferenceBulletPallete;

            localPlaceOffset = from.localPlaceOffset;
            localRandomOffsetRange = from.localRandomOffsetRange;
            localShooterValue = from.localShooterValue;
            localSizeValue = from.localSizeValue;
            localSpeed = from.localSpeed;
            localTargetValue = from.localTargetValue;
        }
    }
}
