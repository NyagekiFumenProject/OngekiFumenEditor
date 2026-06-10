using OngekiFumenEditor.Base.Attributes;
using OngekiFumenEditor.Base.OngekiObjects.Projectiles;
using OngekiFumenEditor.Base.OngekiObjects.Projectiles.Attributes;
using OngekiFumenEditor.Base.OngekiObjects.Projectiles.Enums;
using OngekiFumenEditor.Utils;
using System.Collections.Generic;

namespace OngekiFumenEditor.Base.OngekiObjects.Projectiles
{
    public partial class Bell : OngekiMovableObjectBase, IBulletPalleteReferencable, IProjectile
    {
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
        [BellPropertyBrowserReadOnlyForPalleteIsSet]
        public float Speed
        {
            get => ReferenceBulletPallete?.Speed ?? localSpeed;
            set => Set(ref localSpeed, value);
        }

        private int localRandomOffsetRange = 0;
        [ObjectPropertyBrowserShow]
        [BellPropertyBrowserReadOnlyForPalleteIsSet]
        public int RandomOffsetRange
        {
            get => ReferenceBulletPallete?.RandomOffsetRange ?? localRandomOffsetRange;
            set => Set(ref localRandomOffsetRange, value);
        }

        private int localPlaceOffset = 0;
        [ObjectPropertyBrowserShow]
        [BellPropertyBrowserReadOnlyForPalleteIsSet]
        public int PlaceOffset
        {
            get => ReferenceBulletPallete?.PlaceOffset ?? localPlaceOffset;
            set => Set(ref localPlaceOffset, value);
        }

        private Target localTargetValue = Target.FixField;
        [ObjectPropertyBrowserShow]
        [BellPropertyBrowserReadOnlyForPalleteIsSet]
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
        [BellPropertyBrowserReadOnlyForPalleteIsSet]
        public Shooter ShooterValue
        {
            get => ReferenceBulletPallete?.ShooterValue ?? localShooterValue;
            set => Set(ref localShooterValue, value);
        }

        private BulletSize localSizeValue = BulletSize.Normal;
        [ObjectPropertyBrowserShow]
        [BellPropertyBrowserReadOnlyForPalleteIsSet]
        public BulletSize SizeValue
        {
            get => ReferenceBulletPallete?.SizeValue ?? localSizeValue;
            set => Set(ref localSizeValue, value);
        }

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
