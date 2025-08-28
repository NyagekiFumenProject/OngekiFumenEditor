using OngekiFumenEditor.Base.Attributes;
using OngekiFumenEditor.Base.OngekiObjects.Projectiles.Attributes;
using OngekiFumenEditor.Base.OngekiObjects.Projectiles.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Base.OngekiObjects.Projectiles
{
    public interface IProjectile
    {
        float Speed { get; }

        float RandomOffsetRange { get; }

        int PlaceOffset { get; }

        BulletType TypeValue { get; }

        Target TargetValue { get; }

        Shooter ShooterValue { get; }

        BulletSize SizeValue { get; }

        bool IsEnableSoflan { get; }
    }
}
