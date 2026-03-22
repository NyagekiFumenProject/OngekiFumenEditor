namespace OngekiFumenEditor.Utils.ObjectPool
{
    public interface ICacheCleanable
    {
        void OnBeforeGetClean();
        void OnAfterPutClean();
    }
}
