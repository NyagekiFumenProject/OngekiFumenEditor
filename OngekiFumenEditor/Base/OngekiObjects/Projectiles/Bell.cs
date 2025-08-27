using OngekiFumenEditor.Base.Attributes;
using OngekiFumenEditor.Base.OngekiObjects.Projectiles.Enums;
using System.Collections.Generic;

namespace OngekiFumenEditor.Base.OngekiObjects
{
    public class Bell : OngekiMovableObjectBase, IBulletPalleteReferencable
    {
        private class BellPropertyBrowserReadOnlyForPalleteNotNull : ObjectPropertyBrowserReadOnlyForCondition<Bell>
        {
            public BellPropertyBrowserReadOnlyForPalleteNotNull() : base(b => b.ReferenceBulletPallete != null)
            {
            }
        }

        public static string CommandName => "BEL";
        public override string IDShortName => CommandName;

        public Bell()
        {
            ReferenceBulletPallete = null;
        }

        private BulletPallete referenceBulletPallete;
        public BulletPallete ReferenceBulletPallete
        {
            get { return referenceBulletPallete; }
            set
            {
                if (value is not null)
                    value.PropertyChanged -= ReferenceBulletPallete_PropertyChanged;
                referenceBulletPallete = value;
                if (value is not null)
                    value.PropertyChanged += ReferenceBulletPallete_PropertyChanged;
                NotifyOfPropertyChange(() => ReferenceBulletPallete);

                NotifyOfPropertyChange(() => Speed);
                NotifyOfPropertyChange(() => StrID);
                NotifyOfPropertyChange(() => PlaceOffset);
                NotifyOfPropertyChange(() => TypeValue);
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
        [BellPropertyBrowserReadOnlyForPalleteNotNull]
        public float Speed
        {
            get => ReferenceBulletPallete?.Speed ?? localSpeed;
            set => Set(ref localSpeed, value);
        }

        private float localRandomOffsetRange = 0f;
        [ObjectPropertyBrowserShow]
        [BellPropertyBrowserReadOnlyForPalleteNotNull]
        public float RandomOffsetRange
        {
            get => ReferenceBulletPallete?.RandomOffsetRange ?? localRandomOffsetRange;
            set => Set(ref localRandomOffsetRange, value);
        }

        [ObjectPropertyBrowserAlias("BPL." + nameof(StrID))]
        [ObjectPropertyBrowserShow]
        [BellPropertyBrowserReadOnlyForPalleteNotNull]
        public string StrID => ReferenceBulletPallete?.StrID;

        private string setStrID;
        [ObjectPropertyBrowserShow]
        [ObjectPropertyBrowserTipText("ObjectPalleteStrId")]
        [ObjectPropertyBrowserAlias("SetStrID")]
        public string SetStrID
        {
            get => setStrID;
            set
            {
                Set(ref setStrID, value);
                setStrID = default;
            }
        }

        private int localPlaceOffset = 0;
        [ObjectPropertyBrowserShow]
        [BellPropertyBrowserReadOnlyForPalleteNotNull]
        public int PlaceOffset
        {
            get => ReferenceBulletPallete?.PlaceOffset ?? localPlaceOffset;
            set => Set(ref localPlaceOffset, value);
        }

        private BulletType localTypeValue = BulletType.Circle;
        [ObjectPropertyBrowserShow]
        [BellPropertyBrowserReadOnlyForPalleteNotNull]
        public BulletType TypeValue
        {
            get => ReferenceBulletPallete?.TypeValue ?? localTypeValue;
            set => Set(ref localTypeValue, value);
        }

        private Target localTargetValue = Target.FixField;
        [ObjectPropertyBrowserShow]
        [BellPropertyBrowserReadOnlyForPalleteNotNull]
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
        [BellPropertyBrowserReadOnlyForPalleteNotNull]
        public Shooter ShooterValue
        {
            get => ReferenceBulletPallete?.ShooterValue ?? localShooterValue;
            set => Set(ref localShooterValue, value);
        }

        private BulletSize localSizeValue = BulletSize.Normal;
        [ObjectPropertyBrowserShow]
        [BellPropertyBrowserReadOnlyForPalleteNotNull]
        public BulletSize SizeValue
        {
            get => ReferenceBulletPallete?.SizeValue ?? localSizeValue;
            set => Set(ref localSizeValue, value);
        }

        public bool IsEnableSoflan => ReferenceBulletPallete?.IsEnableSoflan ?? (TargetValue != Target.Player);

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
        }
    }
}
