using OngekiFumenEditor.Base.OngekiObjects;

namespace OngekiFumenEditor.Modules.FumenBulletPalleteListViewer
{
    public interface IPlaceBatchProjectileInfo
    {
        BulletPallete Pallete { get; }
        bool IsBell { get; }
    }
}
