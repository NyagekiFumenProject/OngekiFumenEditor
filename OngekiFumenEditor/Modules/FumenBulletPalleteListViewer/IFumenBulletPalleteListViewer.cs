using Gemini.Framework;

namespace OngekiFumenEditor.Modules.FumenBulletPalleteListViewer
{
    public interface IFumenBulletPalleteListViewer : ITool
    {
        bool IsRequestPlaceBatchProjectilePreview { get; }
        IPlaceBatchProjectileInfo RequestBatchPlaceProjectileInfo { get; }
    }
}
