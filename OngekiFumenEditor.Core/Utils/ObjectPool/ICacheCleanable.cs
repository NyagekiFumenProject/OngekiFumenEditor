namespace OngekiFumenEditor.Core.Utils.ObjectPool
{
    public interface ICacheCleanable
    {
        void OnBeforeGetClean();
        void OnAfterPutClean();
    }
}

