using OngekiFumenEditor.Avalonia.Base.Attributes;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects.Projectiles.Attributes;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects.Projectiles.Enums;

namespace OngekiFumenEditor.Avalonia.Base.OngekiObjects.Projectiles
{
    public partial class Bell : OngekiMovableObjectBase, IBulletPalleteReferencable, IProjectile
    {
        public const string OngekiDefaultBellPaletteName = "--";

        public static string CommandName => "BEL";
        public static string CustomCommandName => "[CUSTOM_BEL]";

        public override string IDShortName => CommandName;

        [LocalizableObjectPropertyBrowserAlias("BulletPalleteDisplayName")]
        public BulletPallete ReferenceBulletPallete
        {
            get;
            set
            {
                //Log.LogDebug($"bell(id:{Id})'s pallete has been changed from {referenceBulletPallete?.StrID} to {value?.StrID}");
                this.RegisterOrUnregisterPropertyChangeEvent(field, value, ReferenceBulletPallete_PropertyChanged);
                SetProperty(ref field, value);

                OnPropertyChanged(nameof(Speed));
                OnPropertyChanged(nameof(PlaceOffset));
                OnPropertyChanged(nameof(TargetValue));
                OnPropertyChanged(nameof(ShooterValue));
                OnPropertyChanged(nameof(RandomOffsetRange));
                OnPropertyChanged(nameof(SizeValue));
                OnPropertyChanged(nameof(IsEnableSoflan));
            }
        } = null;

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
                    OnPropertyChanged(e.PropertyName);
                    break;
            }
        }

        [ObjectPropertyBrowserShow]
        [ProjectilePropertyBrowserReadOnlyForPalleteIsSet]
        public float Speed
        {
            get => ReferenceBulletPallete?.Speed ?? field;
            set => SetProperty(ref field, value);
        } = 1f;

        [ObjectPropertyBrowserShow]
        [ProjectilePropertyBrowserReadOnlyForPalleteIsSet]
        public int RandomOffsetRange
        {
            get => ReferenceBulletPallete?.RandomOffsetRange ?? field;
            set => SetProperty(ref field, value);
        } = 0;

        [ObjectPropertyBrowserShow]
        [ProjectilePropertyBrowserReadOnlyForPalleteIsSet]
        public int PlaceOffset
        {
            get => ReferenceBulletPallete?.PlaceOffset ?? field;
            set => SetProperty(ref field, value);
        } = 0;

        [ObjectPropertyBrowserShow]
        [ProjectilePropertyBrowserReadOnlyForPalleteIsSet]
        public Shooter ShooterValue
        {
            get => ReferenceBulletPallete?.ShooterValue ?? field;
            set => SetProperty(ref field, value);
        } = Shooter.TargetHead;

        [ObjectPropertyBrowserShow]
        [ProjectilePropertyBrowserReadOnlyForPalleteIsSet]
        public Target TargetValue
        {
            get => ReferenceBulletPallete?.TargetValue ?? field;
            set => SetProperty(ref field, value);
        } = Target.FixField;

        [ObjectPropertyBrowserHide] // SizeValue has no effect on bells
        public BulletSize SizeValue
        {
            get => ReferenceBulletPallete?.SizeValue ?? field;
            set => SetProperty(ref field, value);
        } = BulletSize.Normal;

        /// <summary>
        /// 是否受 Soflan 速度变化影响
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

            if (from.ReferenceBulletPallete is null)
            {
                PlaceOffset = from.PlaceOffset;
                RandomOffsetRange = from.RandomOffsetRange;
                ShooterValue = from.ShooterValue;
                Speed = from.Speed;
                TargetValue = from.TargetValue;
                SizeValue = from.SizeValue;
            }
            else
            {
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
