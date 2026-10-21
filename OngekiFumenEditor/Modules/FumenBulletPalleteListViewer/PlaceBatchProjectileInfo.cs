using OngekiFumenEditor.Base.OngekiObjects;

namespace OngekiFumenEditor.Modules.FumenBulletPalleteListViewer
{
    public sealed class PlaceBatchProjectileInfo : IPlaceBatchProjectileInfo
    {
        public PlaceBatchProjectileInfo(BulletPallete pallete, bool isBell)
        {
            Pallete = pallete;
            IsBell = isBell;
        }

        public BulletPallete Pallete { get; }
        public bool IsBell { get; }
    }
}
