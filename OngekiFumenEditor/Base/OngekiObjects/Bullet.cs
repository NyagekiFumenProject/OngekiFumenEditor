using OngekiFumenEditor.Base.EditorObjects;
using OngekiFumenEditor.Modules.FumenObjectPropertyBrowser.Attrbutes;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels.OngekiObjects;
using System;
using System.Collections.Generic;

namespace OngekiFumenEditor.Base.OngekiObjects
{
    //[DontShowPropertyInfoAttrbute]
    public class Bullet : OngekiMovableObjectBase,IBulletPalleteReferencable
    {
        private BulletPallete referenceBulletPallete;
        public BulletPallete ReferenceBulletPallete
        {
            get { return referenceBulletPallete; }
            set
            {
                referenceBulletPallete = value;
                NotifyOfPropertyChange(() => ReferenceBulletPallete);
            }
        }

        public class BulletType : FadeStringEnum
        {
            public BulletType(string value) : base(value)
            {

            }

            /// <summary>
            /// 将使用BULLET_DAMAGE伤害
            /// </summary>
            public static BulletType Normal { get; } = new BulletType("NML");
            /// <summary>
            /// 将使用HARDBULLET_DAMAGE伤害
            /// </summary>
            public static BulletType Hard { get; } = new BulletType("STR");
            /// <summary>
            /// 将使用DANGERBULLET_DAMAGE伤害
            /// </summary>
            public static BulletType Danger { get; } = new BulletType("DNG");
        }

        private BulletPalleteAuxiliaryLine line;

        public Bullet()
        {
            line = new BulletPalleteAuxiliaryLine(this);
        }

        public override IEnumerable<IDisplayableObject> GetDisplayableObjects()
        {
            yield return line;
            yield return this;
        }

        private BulletType bulletTypeValue = BulletType.Normal;
        public BulletType BulletTypeValue
        {
            get { return bulletTypeValue; }
            set
            {
                bulletTypeValue = value;
                NotifyOfPropertyChange(() => BulletTypeValue);
            }
        }

        public override Type ModelViewType => typeof(BulletViewModel);

        public override string IDShortName => CommandName;

        public const string CommandName = "BLT";

        public override string ToString() => $"{base.ToString()} Pallete:({ReferenceBulletPallete})";

        public override void Copy(OngekiObjectBase fromObj, OngekiFumen fumen)
        {
            base.Copy(fromObj, fumen);

            if (fromObj is not Bullet from)
                return;

            ReferenceBulletPallete = from.ReferenceBulletPallete;
            BulletTypeValue = from.BulletTypeValue;
        }
    }
}
