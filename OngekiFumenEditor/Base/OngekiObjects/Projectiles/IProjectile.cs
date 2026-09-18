using OngekiFumenEditor.Base.OngekiObjects.Projectiles.Enums;

namespace OngekiFumenEditor.Base.OngekiObjects.Projectiles
{
    public interface IProjectile
    {
        float Speed { get; }
        int RandomOffsetRange { get; }
        int PlaceOffset { get; }
        BulletType TypeValue { get; }
        Target TargetValue { get; }
        Shooter ShooterValue { get; }
        BulletSize SizeValue { get; }
        bool IsEnableSoflan { get; }
    }
}
